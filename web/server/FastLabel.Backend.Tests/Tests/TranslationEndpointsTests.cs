namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class TranslationEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

    // =========================================================================
    // TESTS POUR GET /api/translations/{language}
    // =========================================================================

    [Fact]
    public async Task GetTranslations_EndToEnd_ReturnsDictionaryForValidLanguage()
    {
        // Arrange : Récupérer un code de langue valide existant en base
        string validLangCode;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            validLangCode = await db.Languages.OrderBy(l => l.Id).Select(l => l.Code).FirstAsync();
        }

        // Act
        var response = await _client.GetAsync($"/api/translations/{validLangCode}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<TranslationDto>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Key));
            Assert.NotNull(t.Value);
        });
    }

    [Fact]
    public async Task GetTranslations_Fail_WithInvalidLanguageCode_ReturnsBadRequest()
    {
        // Act : Code de langue inexistant
        var response = await _client.GetAsync("/api/translations/xx-ZZ");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("language.error.invalid", error?.Message);
    }

    [Fact]
    public async Task GetTranslations_Fail_WithEmptyOrWhitespaceLanguage_ReturnsBadRequest()
    {
        // Act : Code de langue vide ou espaces
        var response = await _client.GetAsync("/api/translations/%20");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("language.error.invalid", error?.Message);
    }
}