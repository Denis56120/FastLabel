namespace FastLabel.Backend.Tests.Tests;

public class LanguageEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    /// <summary>
    /// Helper : Crée un utilisateur actif en base avec une langue spécifique, effectue un login réel et retourne un HttpClient configuré.
    /// </summary>
    private async Task<HttpClient> CreateAuthenticatedClientWithLanguageAsync(string email, int languageId, string password = "ValidPassword123!")
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
                LanguageId = languageId
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
    // 1. TESTS POUR GET /api/languages
    // =========================================================================

    [Fact]
    public async Task GetLanguages_EndToEnd_AnonymousReturnsListWithDefaultOrCookieLanguage()
    {
        // Arrange - Utilisation du client anonyme avec un cookie "language" configuré
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/languages");
        // On suppose qu'une langue "en" ou autre existe, ou on prend la première dispo en base
        request.Headers.Add("Cookie", "language=en");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<LanguageDto>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetLanguages_EndToEnd_AuthenticatedUsesUserLanguage()
    {
        // Arrange - On crée un utilisateur rattaché à une langue spécifique (ex: Id = 1)
        int langId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            langId = await db.Languages.OrderBy(l => l.Id).Select(l => l.Id).FirstAsync();
        }

        var client = await CreateAuthenticatedClientWithLanguageAsync("lang.user@exemple.fr", langId);

        // Act
        var response = await client.GetAsync("/api/languages");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<LanguageDto>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // =========================================================================
    // 2. TESTS POUR GET /api/languages/set
    // =========================================================================

    [Fact]
    public async Task SetLanguage_EndToEnd_SetsCookieSuccessfullyWhenValid()
    {
        // Arrange - Récupérer un code de langue valide existant en base (ex: "fr" ou le premier code dispo)
        string validLangCode;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            validLangCode = await db.Languages.OrderBy(l => l.Id).Select(l => l.Code).FirstAsync();
        }

        // Act
        var response = await _client.GetAsync($"/api/languages/set?language={validLangCode}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Set-Cookie"));
        
        var cookies = response.Headers.GetValues("Set-Cookie").First();
        Assert.Contains("language", cookies);
    }

    [Fact]
    public async Task SetLanguage_Fail_WithInvalidCode_ReturnsBadRequest()
    {
        // Act - Code de langue totalement fantaisiste
        var response = await _client.GetAsync("/api/languages/set?language=xx-XX");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("language.error.invalid", error?.Message);
    }

    [Fact]
    public async Task SetLanguage_Fail_WithEmptyCode_ReturnsBadRequest()
    {
        // Act - Paramètre vide ou blanc
        var response = await _client.GetAsync("/api/languages/set?language=");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("language.error.invalid", error?.Message);
    }
}