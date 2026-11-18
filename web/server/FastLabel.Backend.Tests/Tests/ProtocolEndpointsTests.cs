namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class ProtocolEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

    // =========================================================================
    // TESTS POUR GET /api/protocols
    // =========================================================================

    [Fact]
    public async Task GetProtocols_EndToEnd_ReturnsListForAuthenticatedUser()
    {
        // Arrange
        var client = await factory.CreateAuthenticatedClientAsync("protocol.get@exemple.fr");

        // Act
        var response = await client.GetAsync("/api/protocols");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ProtocolDto>>();
        
        Assert.NotNull(result);
        // S'assure que les protocoles de référence sont bien renvoyés
        Assert.NotEmpty(result);
        Assert.All(result, p => 
        {
            Assert.True(p.Id > 0);
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
        });
    }

    [Fact]
    public async Task GetProtocols_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Act - Utilisation du client non authentifié
        var response = await _client.GetAsync("/api/protocols");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}