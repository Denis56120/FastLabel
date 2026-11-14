namespace FastLabel.Backend.Endpoints;

public static class StripeEndpoints
{
    private static readonly string[] handler = ["Monthly", "Yearly"];

    public static void MapStripeEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("api/stripe/create-checkout-session", async ([FromBody] StripeDto request, AppDbContext db, IConfiguration configuration) =>
        {
            var allowedIntervals = handler;
            if (!allowedIntervals.Contains(request.BillingInterval))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.error.invalid_interval" });
            }

            string expectedTextTag = $"plan.{request.Plan.ToLower()}.name";
            
            var planExists = await db.Plans
                .Include(p => p.Name)
                .AnyAsync(p => p.Name.Tag == expectedTextTag);

            if (!planExists)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.error.plan_not_found" });
            }

            string priceKey = $"Stripe:{request.Plan}{request.BillingInterval}";
            string? priceId = configuration[priceKey];

            if (string.IsNullOrEmpty(priceId))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "stripe.error.price_not_configured" });
            }

            string clientUrl = configuration["Url:ClientUrl"] ?? "https://fastlabel.com";

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
                SuccessUrl = $"{clientUrl}/dashboard/subscription/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{clientUrl}/dashboard/subscription/cancel",
            };

            var service = new Stripe.Checkout.SessionService();
            Stripe.Checkout.Session session = await service.CreateAsync(options);

            return Results.Ok(new { session.Url });
        });
    }
}