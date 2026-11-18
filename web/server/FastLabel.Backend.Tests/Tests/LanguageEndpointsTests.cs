namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class LanguageEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly TestWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

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

        var client = await _factory.CreateAuthenticatedClientAsync("lang.user@exemple.fr", languageId: langId);

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