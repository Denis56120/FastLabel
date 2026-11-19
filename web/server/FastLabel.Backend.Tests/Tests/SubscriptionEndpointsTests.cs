namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class SubscriptionEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

    private async Task<(int planId, int stripeId)> GetValidReferencesAsync(int userId, bool preferPaidPlan = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        int planId;
        if (preferPaidPlan)
        {
            planId = await db.Plans
                .Where(p => p.Code != "free")
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Id)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync()
                ?? await db.Plans.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        }
        else
        {
            planId = await db.Plans.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        }

        var stripeRecord = new StripeRecord
        {
            UserId = userId,
            StripeCustomerId = $"cus_{Guid.NewGuid():N}",
            StripeSubscriptionId = $"sub_{Guid.NewGuid():N}"
        };
        db.Stripes.Add(stripeRecord);
        await db.SaveChangesAsync();

        return (planId, stripeRecord.Id);
    }

    [Fact]
    public async Task GetSubscriptions_EndToEnd_ReturnsListForUser()
    {
        var email = "sub.get@exemple.fr";
        var client = await factory.CreateAuthenticatedClientAsync(email);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var (planId, stripeId) = await GetValidReferencesAsync(user.Id);

            db.Subscriptions.Add(new Subscription
            {
                UserId = user.Id,
                PlanId = planId,
                StripeId = stripeId,
                SubscriptionPeriod = 12,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/account/subscriptions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<SubscriptionDto>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetSubscriptions_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/account/subscriptions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentSubscription_EndToEnd_ReturnsActiveSubscription()
    {
        var email = "sub.current@exemple.fr";
        var client = await factory.CreateAuthenticatedClientAsync(email);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var (planId, stripeId) = await GetValidReferencesAsync(user.Id, preferPaidPlan: true);

            db.Subscriptions.Add(new Subscription
            {
                UserId = user.Id,
                PlanId = planId,
                StripeId = stripeId,
                SubscriptionPeriod = 12,
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddMonths(12),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/account/subscriptions/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(12, result.SubscriptionPeriod);
        Assert.True(result.HasStripeSubscription);
    }

    [Fact]
    public async Task GetCurrentSubscription_EndToEnd_FallsBackToFreeWhenPaidExpired()
    {
        var email = "sub.empty@exemple.fr";
        var client = await factory.CreateAuthenticatedClientAsync(email);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var (paidPlanId, stripeId) = await GetValidReferencesAsync(user.Id, preferPaidPlan: true);

            db.Subscriptions.Add(new Subscription
            {
                UserId = user.Id,
                PlanId = paidPlanId,
                StripeId = stripeId,
                SubscriptionPeriod = 1,
                CurrentPeriodStart = DateTime.UtcNow.AddYears(-2),
                CurrentPeriodEnd = DateTime.UtcNow.AddYears(-2).AddMonths(1),
                CreatedAt = DateTime.UtcNow.AddYears(-2)
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/account/subscriptions/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.NotNull(result);
        Assert.Equal("free", result.PlanCode);
    }

    [Fact]
    public async Task GetCurrentSubscription_EndToEnd_PrioritizesActivePaidOverFree()
    {
        var email = "sub.priority@exemple.fr";
        var client = await factory.CreateAuthenticatedClientAsync(email);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var (paidPlanId, stripeId) = await GetValidReferencesAsync(user.Id, preferPaidPlan: true);

            db.Subscriptions.Add(new Subscription
            {
                UserId = user.Id,
                PlanId = paidPlanId,
                StripeId = stripeId,
                SubscriptionPeriod = 1,
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/account/subscriptions/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.NotNull(result);
        Assert.NotEqual("free", result.PlanCode);
    }

    [Fact]
    public async Task GetCurrentSubscription_EndToEnd_UsesExplicitCurrentPeriodEndWhenPresent()
    {
        var email = "sub.period@exemple.fr";
        var client = await factory.CreateAuthenticatedClientAsync(email);
        var expectedEnd = DateTime.UtcNow.AddDays(7);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var (paidPlanId, stripeId) = await GetValidReferencesAsync(user.Id, preferPaidPlan: true);

            db.Subscriptions.Add(new Subscription
            {
                UserId = user.Id,
                PlanId = paidPlanId,
                StripeId = stripeId,
                SubscriptionPeriod = 1,
                CurrentPeriodStart = DateTime.UtcNow.AddDays(-23),
                CurrentPeriodEnd = expectedEnd,
                CreatedAt = DateTime.UtcNow.AddMonths(-5)
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/account/subscriptions/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.NotNull(result);
        Assert.NotEqual("free", result.PlanCode);
        Assert.NotNull(result.CurrentPeriodEnd);
        Assert.True(Math.Abs((result.CurrentPeriodEnd.Value - expectedEnd).TotalSeconds) < 5);
    }

    [Fact]
    public async Task GetCurrentSubscription_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/account/subscriptions/current");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}