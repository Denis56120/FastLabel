namespace FastLabel.Backend.Endpoints;

public static class StripeEndpoints
{
    private static readonly string[] AllowedIntervals = ["Monthly", "Yearly"];

    public static void MapStripeEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("api/stripe/create-checkout-session", async ([FromBody] StripeDto request, AppDbContext db, IConfiguration configuration, ClaimsPrincipal claimsUser) =>
        {
            if (string.IsNullOrWhiteSpace(request.BillingInterval) || !AllowedIntervals.Contains(request.BillingInterval))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.error.invalid_interval" });
            }

            if (string.IsNullOrWhiteSpace(request.Plan))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.error.plan_not_found" });
            }

            string normalizedCode = request.Plan.Trim().ToLowerInvariant();
            string expectedTextTag = $"plan.{normalizedCode}.name";

            var plan = await DbHelpers.GetPlanByTagAsync(db, expectedTextTag)
                ?? await db.Plans.FirstOrDefaultAsync(p => p.Code.ToLower() == normalizedCode);

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

            var secretKey = configuration["Stripe:SecretKey"];
            if (!string.IsNullOrWhiteSpace(secretKey))
            {
                Stripe.StripeConfiguration.ApiKey = secretKey;
            }

            var clientUrl = ConfigurationHelpers.GetClientUrl(configuration);
            var (userId, _, _) = claimsUser.GetUserInfo();

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
                ClientReferenceId = userId > 0 ? userId.ToString() : null,
                SuccessUrl = $"{clientUrl}/dashboard/subscription/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{clientUrl}/dashboard/subscription/cancel",
            };

            try
            {
                var service = new Stripe.Checkout.SessionService();
                Stripe.Checkout.Session session = await service.CreateAsync(options);

                return Results.Ok(new { session.Url });
            }
            catch (Stripe.StripeException)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.error.checkout_failed" });
            }
        });

        routes.MapPost("api/stripe/webhook", async (HttpRequest request, AppDbContext db, IConfiguration configuration) =>
        {
            var json = await new StreamReader(request.Body).ReadToEndAsync();
            var webhookSecret = configuration["Stripe:WebhookSecret"];
            var secretKey = configuration["Stripe:SecretKey"];

            if (!string.IsNullOrWhiteSpace(secretKey))
            {
                Stripe.StripeConfiguration.ApiKey = secretKey;
            }

            Stripe.Event stripeEvent;
            try
            {
                var signatureHeader = request.Headers["Stripe-Signature"];
                stripeEvent = !string.IsNullOrWhiteSpace(webhookSecret)
                    ? Stripe.EventUtility.ConstructEvent(json, signatureHeader, webhookSecret)
                    : Stripe.EventUtility.ParseEvent(json);
            }
            catch (Stripe.StripeException)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.webhook.error.invalid_signature" });
            }

            switch (stripeEvent.Type)
            {
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

            var periodMonths = string.Equals(plan.Code, "yearly", StringComparison.OrdinalIgnoreCase) ? 12 : 1;

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