namespace FastLabel.Backend.Endpoints;

public static class StripeEndpoints
{
    private static readonly string[] AllowedIntervals = ["Monthly", "Yearly"];

    public static void MapStripeEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("api/stripe/create-checkout-session", async (
            [FromBody] StripeDto request,
            AppDbContext db,
            IConfiguration configuration,
            ClaimsPrincipal claimsUser) =>
        {
            var (userId, _, _) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.BillingInterval) || !AllowedIntervals.Contains(request.BillingInterval))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.error.invalid_interval" });
            }

            if (string.IsNullOrWhiteSpace(request.Plan))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.error.plan_not_found" });
            }

            string normalizedCode = request.Plan.Trim().ToLowerInvariant();

            var plan = await db.Plans
                .FirstOrDefaultAsync(p => p.Code.ToLower() == normalizedCode);

            if (plan == null)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.error.plan_not_found" });
            }

            string priceKey = $"Stripe:{request.Plan}{request.BillingInterval}";
            string? priceId = configuration[priceKey]
                ?? (string.Equals(request.BillingInterval, "Yearly", StringComparison.OrdinalIgnoreCase)
                    ? plan.StripeYearlyPriceId
                    : plan.StripeMonthlyPriceId);

            if (string.IsNullOrEmpty(priceId))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.error.price_not_configured" });
            }

            var clientUrl = ConfigurationHelpers.GetClientUrl(configuration);

            var options = new Stripe.Checkout.SessionCreateOptions
            {
                LineItems =
                [
                    new Stripe.Checkout.SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1,
                    },
                ],
                Mode = "subscription",
                ClientReferenceId = userId.ToString(),
                SuccessUrl = $"{clientUrl}/dashboard/subscription/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{clientUrl}/dashboard/subscription/cancel",
            };

            try
            {
                var service = new Stripe.Checkout.SessionService();
                Stripe.Checkout.Session session = await service.CreateAsync(options);

                return Results.Ok(new { session.Url });
            }
            catch (Stripe.StripeException ex)
            {
                AppLog.Error(ex);
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.error.checkout_failed" });
            }
        }).RequireAuthorization();

        routes.MapPost("api/stripe/webhook", async (HttpRequest request, AppDbContext db, IConfiguration configuration) =>
        {
            var json = await new StreamReader(request.Body).ReadToEndAsync();
            var webhookSecret = configuration["STRIPE_WEBHOOK_SECRET"];

            var signatureHeader = request.Headers["Stripe-Signature"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(webhookSecret) || string.IsNullOrWhiteSpace(signatureHeader))
            {
                var ex = new InvalidOperationException("stripe.webhook.error.missing_signature_or_secret");
                AppLog.Error(ex);
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.webhook.error.invalid_signature" });
            }

            Stripe.Event stripeEvent;
            try
            {
                stripeEvent = Stripe.EventUtility.ConstructEvent(json, signatureHeader, webhookSecret, throwOnApiVersionMismatch: false);
            }
            catch (Stripe.StripeException ex)
            {
                AppLog.Error(ex);
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.webhook.error.invalid_signature" });
            }

            switch (stripeEvent.Type)
            {
                case Stripe.EventTypes.CustomerSubscriptionUpdated:
                    if (stripeEvent.Data.Object is Stripe.Subscription updatedSubscription)
                    {
                        await HandleCustomerSubscriptionUpdatedAsync(updatedSubscription, db);
                    }
                    break;

                case Stripe.EventTypes.InvoicePaid:
                    if (stripeEvent.Data.Object is Stripe.Invoice paidInvoice)
                    {
                        await HandleInvoicePaidAsync(paidInvoice, db);
                    }
                    break;

                case Stripe.EventTypes.InvoicePaymentFailed:
                    if (stripeEvent.Data.Object is Stripe.Invoice failedInvoice)
                    {
                        await HandleInvoicePaymentFailedAsync(failedInvoice, db);
                    }
                    break;

                case Stripe.EventTypes.CustomerSubscriptionDeleted:
                    if (stripeEvent.Data.Object is Stripe.Subscription deletedSubscription)
                    {
                        await HandleCustomerSubscriptionDeletedAsync(deletedSubscription, db);
                    }
                    break;
            }

            return Results.Ok();
        }).DisableAntiforgery();
    }

    private static async Task HandleCustomerSubscriptionUpdatedAsync(Stripe.Subscription updatedSubscription, AppDbContext db)
    {
        var stripeRecord = await db.Stripes
            .FirstOrDefaultAsync(s =>
                (!string.IsNullOrWhiteSpace(updatedSubscription.Id) && s.StripeSubscriptionId == updatedSubscription.Id) ||
                (!string.IsNullOrWhiteSpace(updatedSubscription.CustomerId) && s.StripeCustomerId == updatedSubscription.CustomerId));

        if (stripeRecord == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(updatedSubscription.Id) &&
            !string.Equals(stripeRecord.StripeSubscriptionId, updatedSubscription.Id, StringComparison.Ordinal))
        {
            stripeRecord.StripeSubscriptionId = updatedSubscription.Id;
        }

        var firstItem = updatedSubscription.Items?.Data?.FirstOrDefault();
        var priceId = firstItem?.Price?.Id;
        if (string.IsNullOrWhiteSpace(priceId))
        {
            return;
        }

        var targetPlan = await DbHelpers.GetPlanByPriceIdAsync(db, priceId);
        if (targetPlan == null)
        {
            return;
        }

        bool isYearly = !string.IsNullOrWhiteSpace(targetPlan.StripeYearlyPriceId) &&
                        string.Equals(targetPlan.StripeYearlyPriceId, priceId, StringComparison.OrdinalIgnoreCase);
        int targetPeriodMonths = isYearly ? 12 : 1;

        var now = DateTime.UtcNow;
        var periodStart = firstItem?.CurrentPeriodStart != null
            ? firstItem.CurrentPeriodStart.ToUniversalTime()
            : now;
        var periodEnd = firstItem?.CurrentPeriodEnd != null
            ? firstItem.CurrentPeriodEnd.ToUniversalTime()
            : now.AddMonths(targetPeriodMonths);

        var subscription = await db.Subscriptions
            .Where(s => s.UserId == stripeRecord.UserId && s.StripeId == stripeRecord.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (subscription == null)
        {
            subscription = await DbHelpers.CreateSubscription(
                db,
                stripeRecord.UserId,
                targetPlan.Code,
                targetPeriodMonths,
                stripeRecord.Id,
                periodStart,
                periodEnd,
                0);
            db.Subscriptions.Add(subscription);
        }
        else
        {
            var currentPlan = await db.Plans.FirstOrDefaultAsync(p => p.Id == subscription.PlanId);
            if (currentPlan != null && targetPlan.Id != currentPlan.Id)
            {
                bool isHigherPlan = targetPlan.SortOrder > currentPlan.SortOrder;
                bool isValidPeriod = targetPeriodMonths >= subscription.SubscriptionPeriod;
                if (!isHigherPlan || !isValidPeriod)
                {
                    var ex = new InvalidOperationException("stripe.webhook.error.invalid_plan_upgrade");
                    AppLog.Error(ex);
                    return;
                }
            }

            subscription.PlanId = targetPlan.Id;
            subscription.SubscriptionPeriod = targetPeriodMonths;
            subscription.CurrentPeriodStart = periodStart;
            subscription.CurrentPeriodEnd = periodEnd;
            subscription.UpdatedAt = now;
        }

        await db.SaveChangesAsync();
    }

    private static async Task HandleInvoicePaidAsync(Stripe.Invoice invoice, AppDbContext db)
    {
        var stripeSubscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
        var stripeCustomerId = invoice.CustomerId ?? invoice.Customer?.Id;

        var stripeRecord = await db.Stripes
            .FirstOrDefaultAsync(s =>
                (!string.IsNullOrWhiteSpace(stripeSubscriptionId) && s.StripeSubscriptionId == stripeSubscriptionId) ||
                (!string.IsNullOrWhiteSpace(stripeCustomerId) && s.StripeCustomerId == stripeCustomerId));

        if (stripeRecord == null)
        {
            return;
        }

        var now = DateTime.UtcNow;

        var subscription = await db.Subscriptions
            .Where(s => s.UserId == stripeRecord.UserId && s.StripeId == stripeRecord.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        var firstLine = invoice.Lines?.Data?.FirstOrDefault();
        var periodStart = firstLine?.Period?.Start != null
            ? firstLine.Period.Start.ToUniversalTime()
            : now;
        var periodEnd = firstLine?.Period?.End != null
            ? firstLine.Period.End.ToUniversalTime()
            : (subscription != null && subscription.SubscriptionPeriod > 0
                ? now.AddMonths(subscription.SubscriptionPeriod)
                : now.AddMonths(1));

        if (subscription == null)
        {
            var priceId = firstLine?.Pricing?.PriceDetails?.PriceId;
            var plan = await DbHelpers.GetPlanByPriceIdAsync(db, priceId);
            if (plan == null)
            {
                return;
            }

            bool isYearly = !string.IsNullOrWhiteSpace(plan.StripeYearlyPriceId) &&
                            string.Equals(plan.StripeYearlyPriceId, priceId, StringComparison.OrdinalIgnoreCase);
            int periodMonths = isYearly ? 12 : 1;

            subscription = await DbHelpers.CreateSubscription(
                db,
                stripeRecord.UserId,
                plan.Code,
                periodMonths,
                stripeRecord.Id,
                periodStart,
                periodEnd,
                0);
            db.Subscriptions.Add(subscription);
        }
        else
        {
            subscription.CurrentPeriodStart = periodStart;
            subscription.CurrentPeriodEnd = periodEnd;
            subscription.UpdatedAt = now;
        }

        await db.SaveChangesAsync();
    }

    private static async Task HandleInvoicePaymentFailedAsync(Stripe.Invoice invoice, AppDbContext db)
    {
        var stripeSubscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
        var stripeCustomerId = invoice.CustomerId ?? invoice.Customer?.Id;

        var stripeRecord = await db.Stripes
            .FirstOrDefaultAsync(s =>
                (!string.IsNullOrWhiteSpace(stripeSubscriptionId) && s.StripeSubscriptionId == stripeSubscriptionId) ||
                (!string.IsNullOrWhiteSpace(stripeCustomerId) && s.StripeCustomerId == stripeCustomerId));

        if (stripeRecord == null)
        {
            return;
        }

        var subscription = await db.Subscriptions
            .Where(s => s.UserId == stripeRecord.UserId && s.StripeId == stripeRecord.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (subscription == null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!subscription.CurrentPeriodEnd.HasValue || subscription.CurrentPeriodEnd <= now)
        {
            subscription.CurrentPeriodEnd = now.AddDays(7);
        }
        subscription.UpdatedAt = now;

        await db.SaveChangesAsync();
    }

    private static async Task HandleCustomerSubscriptionDeletedAsync(Stripe.Subscription deletedSubscription, AppDbContext db)
    {
        var stripeRecord = await db.Stripes
            .FirstOrDefaultAsync(s =>
                (!string.IsNullOrWhiteSpace(deletedSubscription.Id) && s.StripeSubscriptionId == deletedSubscription.Id) ||
                (!string.IsNullOrWhiteSpace(deletedSubscription.CustomerId) && s.StripeCustomerId == deletedSubscription.CustomerId));

        if (stripeRecord == null)
        {
            return;
        }

        var now = DateTime.UtcNow;

        var currentSubscriptions = await db.Subscriptions
            .Where(s => s.UserId == stripeRecord.UserId && s.StripeId == stripeRecord.Id)
            .ToListAsync();

        foreach (var sub in currentSubscriptions)
        {
            if (!sub.CurrentPeriodEnd.HasValue || sub.CurrentPeriodEnd > now)
            {
                sub.CurrentPeriodEnd = now;
            }
            sub.UpdatedAt = now;
        }

        var freePlanId = await DbHelpers.GetPlanIdByCodeAsync(db, "free");
        var hasFreeSubscription = await db.Subscriptions
            .AnyAsync(s => s.UserId == stripeRecord.UserId && s.PlanId == freePlanId);

        if (!hasFreeSubscription)
        {
            var freeSubscription = await DbHelpers.CreateSubscription(
                db,
                stripeRecord.UserId,
                "free",
                0,
                null,
                now,
                null,
                0);
            db.Subscriptions.Add(freeSubscription);
        }

        await db.SaveChangesAsync();
    }
}