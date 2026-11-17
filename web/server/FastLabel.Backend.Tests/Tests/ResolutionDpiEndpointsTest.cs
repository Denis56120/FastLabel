namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class ResolutionDpiEndpointsTests(CustomWebApplicationFactory factory)
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

    // =========================================================================
    // TESTS POUR GET /api/resolutions
    // =========================================================================

    [Fact]
    public async Task GetResolutions_EndToEnd_ReturnsListForAuthenticatedUser()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("resolution.get@exemple.fr");

        // Act
        var response = await client.GetAsync("/api/resolutions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ResolutionDpiDto>>();
        
        Assert.NotNull(result);
        // S'assure que les résolutions de référence sont bien renvoyées
        Assert.NotEmpty(result);
        Assert.All(result, r => 
        {
            Assert.True(r.Id > 0);
            Assert.True(r.Value > 0);
        });
    }

    [Fact]
    public async Task GetResolutions_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Act - Utilisation du client anonyme non authentifié
        var response = await _client.GetAsync("/api/resolutions");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}