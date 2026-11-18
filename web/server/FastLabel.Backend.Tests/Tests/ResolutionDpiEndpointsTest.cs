namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class ResolutionDpiEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly TestWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

    // =========================================================================
    // TESTS POUR GET /api/resolutions
    // =========================================================================

    [Fact]
    public async Task GetResolutions_EndToEnd_ReturnsListForAuthenticatedUser()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync("resolution.get@exemple.fr");

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