namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class SubscriptionEndpointsTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    /// <summary>
    /// Helper : Crée un utilisateur actif en base, effectue un login réel et retourne un HttpClient configuré avec son cookie de session.
    /// </summary>
    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password = "ValidPassword123!")
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var activeStatus = await db.BasicStatuses
                .Join(db.Texts, bs => bs.NameTextId, t => t.Id, (bs, t) => new { bs, t.Tag })
                .Where(x => x.Tag == "status.active")
                .OrderBy(x => x.bs.Id)
                .Select(x => x.bs)
                .FirstAsync();

            var user = new User
            {
                Name = $"User {email.Split('@')[0]}",
                Email = email,
                PasswordHash = new PasswordHasher<User>().HashPassword(null!, password),
                BasicStatusId = activeStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies));

        client.DefaultRequestHeaders.Add("Cookie", cookies.First().Split(';')[0]);

        return client;
    }

    /// <summary>
    /// Helper pour récupérer des IDs valides de Plan et de SubscriptionType, ainsi que créer un StripeRecord lié à l'utilisateur.
    /// </summary>
    private async Task<(int planId, int typeId, int stripeId)> GetValidReferencesAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var planId = await db.Plans.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        var typeId = await db.SubscriptionTypes.OrderBy(st => st.Id).Select(st => st.Id).FirstAsync();

        // Crée le StripeRecord lié au bon UserId pour satisfaire la contrainte FK_Stripes_Users_UserId
        var stripeRecord = new StripeRecord
        {
            UserId = userId,
            StripeCustomerId = $"cus_{Guid.NewGuid():N}",
            StripeSubscriptionId = $"sub_{Guid.NewGuid():N}"
        };
        db.Stripes.Add(stripeRecord);
        await db.SaveChangesAsync();

        return (planId, typeId, stripeRecord.Id);
    }

    // =========================================================================
    // 1. TESTS POUR GET /api/account/subscriptions
    // =========================================================================

    [Fact]
    public async Task GetSubscriptions_EndToEnd_ReturnsListForUser()
    {
        var email = "sub.get@exemple.fr";
        var client = await CreateAuthenticatedClientAsync(email);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var (planId, typeId, stripeId) = await GetValidReferencesAsync(user.Id);

            db.Subscriptions.Add(new Subscription
            {
                UserId = user.Id,
                PlanId = planId,
                SubscriptionTypeId = typeId,
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

    // =========================================================================
    // 2. TESTS POUR GET /api/account/subscriptions/current
    // =========================================================================

    [Fact]
    public async Task GetCurrentSubscription_EndToEnd_ReturnsActiveSubscription()
    {
        var email = "sub.current@exemple.fr";
        var client = await CreateAuthenticatedClientAsync(email);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var (planId, typeId, stripeId) = await GetValidReferencesAsync(user.Id);

            db.Subscriptions.Add(new Subscription
            {
                UserId = user.Id,
                PlanId = planId,
                SubscriptionTypeId = typeId,
                StripeId = stripeId,
                SubscriptionPeriod = 12, // 12 mois à partir de maintenant (donc valide)
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
    }

    [Fact]
    public async Task GetCurrentSubscription_EndToEnd_ReturnsEmptyDtoWhenNoneActive()
    {
        var email = "sub.empty@exemple.fr";
        var client = await CreateAuthenticatedClientAsync(email);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var (planId, typeId, stripeId) = await GetValidReferencesAsync(user.Id);

            // Abonnement expiré (créé il y a 2 ans, période de 1 mois)
            db.Subscriptions.Add(new Subscription
            {
                UserId = user.Id,
                PlanId = planId,
                SubscriptionTypeId = typeId,
                StripeId = stripeId,
                SubscriptionPeriod = 1,
                CreatedAt = DateTime.UtcNow.AddYears(-2)
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/account/subscriptions/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.NotNull(result);
        Assert.Equal(0, result.Id);
        Assert.Equal(string.Empty, result.PlanName);
    }

    [Fact]
    public async Task GetCurrentSubscription_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/account/subscriptions/current");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}