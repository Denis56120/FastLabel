namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class StripeEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

    private static async Task<User> CreateTestUserAsync(AppDbContext db, string email, string name)
    {
        var activeStatusId = await DbHelpers.GetStatusIdByTagAsync(db, "status.active");
        var defaultLanguage = await db.Languages.OrderBy(l => l.Id).FirstAsync();

        var user = new User
        {
            Email = email,
            Name = name,
            PasswordHash = "hash",
            BasicStatusId = activeStatusId,
            LanguageId = defaultLanguage.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CreateCheckoutSession_Fail_WithInvalidInterval_ReturnsBadRequest()
    {
        var dto = new StripeDto
        {
            Plan = "Pro",
            BillingInterval = "Daily"
        };

        var response = await _client.PostAsJsonAsync("/api/stripe/create-checkout-session", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("stripe.error.invalid_interval", error?.Message);
    }

    [Fact]
    public async Task CreateCheckoutSession_Fail_WhenPlanNotFound_ReturnsBadRequest()
    {
        var dto = new StripeDto
        {
            Plan = "InexistantPlan",
            BillingInterval = "Monthly"
        };

        var response = await _client.PostAsJsonAsync("/api/stripe/create-checkout-session", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("stripe.error.plan_not_found", error?.Message);
    }

    [Fact]
    public async Task CreateCheckoutSession_Fail_WhenPriceNotConfigured_ReturnsBadRequest()
    {
        string planCode;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var plan = await db.Plans.FirstOrDefaultAsync(p => p.Code != "free")
                ?? await db.Plans.FirstAsync();

            plan.StripeMonthlyPriceId = null;
            plan.StripeYearlyPriceId = null;
            await db.SaveChangesAsync();
            planCode = plan.Code;
        }

        var dto = new StripeDto
        {
            Plan = planCode,
            BillingInterval = "Monthly"
        };

        var response = await _client.PostAsJsonAsync("/api/stripe/create-checkout-session", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("stripe.error.price_not_configured", error?.Message);
    }

    [Fact]
    public async Task Webhook_EndToEnd_InvoicePaid_UpdatesSubscriptionPeriod()
    {
        var email = "webhook.paid@exemple.fr";
        var subId = $"sub_{Guid.NewGuid():N}";
        var cusId = $"cus_{Guid.NewGuid():N}";
        var periodStart = DateTime.UtcNow.AddDays(-1);
        var periodEnd = DateTime.UtcNow.AddMonths(1);
        int subscriptionId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await CreateTestUserAsync(db, email, "Webhook Paid User");

            var stripeRecord = new StripeRecord
            {
                UserId = user.Id,
                StripeCustomerId = cusId,
                StripeSubscriptionId = subId
            };
            db.Stripes.Add(stripeRecord);
            await db.SaveChangesAsync();

            var planId = await db.Plans
                .Where(p => p.Code != "free")
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync()
                ?? await db.Plans.Select(p => p.Id).FirstAsync();

            var sub = new Subscription
            {
                UserId = user.Id,
                PlanId = planId,
                StripeId = stripeRecord.Id,
                SubscriptionPeriod = 1,
                CurrentPeriodStart = DateTime.UtcNow.AddMonths(-1),
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddMonths(-1)
            };
            db.Subscriptions.Add(sub);
            await db.SaveChangesAsync();
            subscriptionId = sub.Id;
        }

        var payload = $$"""
        {
          "id": "evt_{{Guid.NewGuid():N}}",
          "object": "event",
          "api_version": "{{Stripe.StripeConfiguration.ApiVersion}}",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_{{Guid.NewGuid():N}}",
              "object": "invoice",
              "customer": "{{cusId}}",
              "parent": {
                "subscription_details": {
                  "subscription": "{{subId}}"
                }
              },
              "lines": {
                "data": [
                  {
                    "period": {
                      "start": {{new DateTimeOffset(periodStart).ToUnixTimeSeconds()}},
                      "end": {{new DateTimeOffset(periodEnd).ToUnixTimeSeconds()}}
                    }
                  }
                ]
              }
            }
          }
        }
        """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/stripe/webhook", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.Subscriptions.FirstAsync(s => s.Id == subscriptionId);
        Assert.NotNull(updated.CurrentPeriodEnd);
        Assert.True(Math.Abs((updated.CurrentPeriodEnd.Value - periodEnd).TotalSeconds) < 2);
    }

    [Fact]
    public async Task Webhook_EndToEnd_InvoicePaymentFailed_SetsGracePeriodEnd()
    {
        var email = "webhook.failed@exemple.fr";
        var subId = $"sub_{Guid.NewGuid():N}";
        var cusId = $"cus_{Guid.NewGuid():N}";
        var beforeCall = DateTime.UtcNow;
        int subscriptionId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await CreateTestUserAsync(db, email, "Webhook Failed User");

            var stripeRecord = new StripeRecord
            {
                UserId = user.Id,
                StripeCustomerId = cusId,
                StripeSubscriptionId = subId
            };
            db.Stripes.Add(stripeRecord);
            await db.SaveChangesAsync();

            var planId = await db.Plans
                .Where(p => p.Code != "free")
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync()
                ?? await db.Plans.Select(p => p.Id).FirstAsync();

            var sub = new Subscription
            {
                UserId = user.Id,
                PlanId = planId,
                StripeId = stripeRecord.Id,
                SubscriptionPeriod = 1,
                CurrentPeriodStart = DateTime.UtcNow.AddMonths(-1),
                CurrentPeriodEnd = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow.AddMonths(-1)
            };
            db.Subscriptions.Add(sub);
            await db.SaveChangesAsync();
            subscriptionId = sub.Id;
        }

        var payload = $$"""
        {
          "id": "evt_{{Guid.NewGuid():N}}",
          "object": "event",
          "api_version": "{{Stripe.StripeConfiguration.ApiVersion}}",
          "type": "invoice.payment_failed",
          "data": {
            "object": {
              "id": "in_{{Guid.NewGuid():N}}",
              "object": "invoice",
              "customer": "{{cusId}}",
              "parent": {
                "subscription_details": {
                  "subscription": "{{subId}}"
                }
              }
            }
          }
        }
        """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/stripe/webhook", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.Subscriptions.FirstAsync(s => s.Id == subscriptionId);
        Assert.NotNull(updated.CurrentPeriodEnd);
        Assert.True(updated.CurrentPeriodEnd.Value > beforeCall.AddDays(6));
    }

    [Fact]
    public async Task Webhook_EndToEnd_CustomerSubscriptionDeleted_EndsSubscriptionAndCreatesFreePlan()
    {
        var email = "webhook.deleted@exemple.fr";
        var subId = $"sub_{Guid.NewGuid():N}";
        var cusId = $"cus_{Guid.NewGuid():N}";
        int userId;
        int paidSubscriptionId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await CreateTestUserAsync(db, email, "Webhook Deleted User");
            userId = user.Id;

            var stripeRecord = new StripeRecord
            {
                UserId = user.Id,
                StripeCustomerId = cusId,
                StripeSubscriptionId = subId
            };
            db.Stripes.Add(stripeRecord);
            await db.SaveChangesAsync();

            var planId = await db.Plans
                .Where(p => p.Code != "free")
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync()
                ?? await db.Plans.Select(p => p.Id).FirstAsync();

            var sub = new Subscription
            {
                UserId = user.Id,
                PlanId = planId,
                StripeId = stripeRecord.Id,
                SubscriptionPeriod = 1,
                CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(20),
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };
            db.Subscriptions.Add(sub);
            await db.SaveChangesAsync();
            paidSubscriptionId = sub.Id;
        }

        var payload = $$"""
        {
          "id": "evt_{{Guid.NewGuid():N}}",
          "object": "event",
          "api_version": "{{Stripe.StripeConfiguration.ApiVersion}}",
          "type": "customer.subscription.deleted",
          "data": {
            "object": {
              "id": "{{subId}}",
              "object": "subscription",
              "customer": "{{cusId}}"
            }
          }
        }
        """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/stripe/webhook", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var paidSub = await verifyDb.Subscriptions.FirstAsync(s => s.Id == paidSubscriptionId);
        Assert.NotNull(paidSub.CurrentPeriodEnd);
        Assert.True(paidSub.CurrentPeriodEnd.Value <= DateTime.UtcNow.AddSeconds(5));

        var freePlanId = await DbHelpers.GetPlanIdByCodeAsync(verifyDb, "free");
        var hasFreeSub = await verifyDb.Subscriptions.AnyAsync(s => s.UserId == userId && s.PlanId == freePlanId);
        Assert.True(hasFreeSub);
    }
}