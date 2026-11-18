namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class AgentTypeEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

    // =========================================================================
    // TESTS POUR GET /api/agenttypes
    // =========================================================================

    [Fact]
    public async Task GetAgentTypes_EndToEnd_ReturnsListForUser()
    {
        // Arrange
        var client = await factory.CreateAuthenticatedClientAsync("agenttypes.e2e@exemple.fr");

        // Act
        var response = await client.GetAsync("/api/agenttypes");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<AgentTypeDto>>();
        
        Assert.NotNull(result);
        // S'assure qu'au moins un type d'agent est renvoyé (données de référence de l'application)
        Assert.NotEmpty(result);
        Assert.All(result, at => 
        {
            Assert.True(at.Id > 0);
            Assert.False(string.IsNullOrWhiteSpace(at.Name));
        });
    }

    [Fact]
    public async Task GetAgentTypes_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Act - Utilisation du client anonyme global _client sans login
        var response = await _client.GetAsync("/api/agenttypes");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}