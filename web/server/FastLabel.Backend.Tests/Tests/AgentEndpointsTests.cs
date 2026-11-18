namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class AgentEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

    // =========================================================================
    // 1. TESTS POUR GET /api/agents
    // =========================================================================

    [Fact]
    public async Task GetAgents_EndToEnd_ReturnsPaginatedListForUser()
    {
        var email = "get.e2e@exemple.fr";
        var client = await factory.CreateAuthenticatedClientAsync(email);
        var uniqueKey = Guid.NewGuid().ToString("N");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var validTypeId = await db.AgentTypes.OrderBy(at => at.Id).Select(at => at.Id).FirstAsync();

            db.Agents.Add(new Agent { AgentTypeId = validTypeId, UserId = user.Id, ApiKey = uniqueKey });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/agents?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<AgentDto>>();
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.Contains(result.Items, a => a.ApiKey == uniqueKey);
    }

    [Fact]
    public async Task GetAgents_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/agents?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAgents_Fail_Isolation_RegularUserDoesNotSeeOthers()
    {
        var client1 = await factory.CreateAuthenticatedClientAsync("get.user1@exemple.fr");
        var client2 = await factory.CreateAuthenticatedClientAsync("get.user2@exemple.fr");
        var uniqueKey = Guid.NewGuid().ToString("N");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user1 = await db.Users.FirstAsync(u => u.Email == "get.user1@exemple.fr");
            var validTypeId = await db.AgentTypes.OrderBy(at => at.Id).Select(at => at.Id).FirstAsync();

            db.Agents.Add(new Agent { AgentTypeId = validTypeId, UserId = user1.Id, ApiKey = uniqueKey });
            await db.SaveChangesAsync();
        }

        var response = await client2.GetAsync("/api/agents?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<AgentDto>>();
        Assert.NotNull(result);
        Assert.DoesNotContain(result.Items, a => a.ApiKey == uniqueKey);
    }

    // =========================================================================
    // 2. TESTS POUR POST /api/agents
    // =========================================================================

    [Fact]
    public async Task PostAgent_EndToEnd_CreatesAgentSuccessfully()
    {
        var client = await factory.CreateAuthenticatedClientAsync("post.e2e@exemple.fr");
        int validTypeId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            validTypeId = await db.AgentTypes.OrderBy(at => at.Id).Select(at => at.Id).FirstAsync();
        }

        var dto = new AgentDto { AgentTypeId = validTypeId };

        var response = await client.PostAsJsonAsync("/api/agents", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AgentDto>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.False(string.IsNullOrWhiteSpace(created.ApiKey));
    }

    [Fact]
    public async Task PostAgent_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/agents", new AgentDto { AgentTypeId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAgent_Fail_WithInvalidType_ReturnsBadRequest()
    {
        var client = await factory.CreateAuthenticatedClientAsync("post.fail@exemple.fr");
        var dto = new AgentDto { AgentTypeId = 999999 };

        var response = await client.PostAsJsonAsync("/api/agents", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("agents.error.invalid_type", error?.Message);
    }

    // =========================================================================
    // 3. TESTS POUR PUT /api/agents/{id}
    // =========================================================================

    [Fact]
    public async Task PutAgent_EndToEnd_UpdatesAgentSuccessfully()
    {
        var email = "put.e2e@exemple.fr";
        var client = await factory.CreateAuthenticatedClientAsync(email);
        int typeId1, typeId2;
        int agentId;
        var uniqueKey = Guid.NewGuid().ToString("N");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var types = await db.AgentTypes.OrderBy(at => at.Id).Take(2).ToListAsync();
            
            if (types.Count < 2)
            {
                var text = new Text { Tag = "agent.type.extra", DefaultTranslation = "Extra" };
                db.Texts.Add(text);
                await db.SaveChangesAsync();
                var extraType = new AgentType { NameTextId = text.Id };
                db.AgentTypes.Add(extraType);
                await db.SaveChangesAsync();
                types.Add(extraType);
            }

            typeId1 = types[0].Id;
            typeId2 = types[1].Id;

            var agent = new Agent { AgentTypeId = typeId1, UserId = user.Id, ApiKey = uniqueKey };
            db.Agents.Add(agent);
            await db.SaveChangesAsync();
            agentId = agent.Id;
        }

        var updateDto = new AgentDto { Id = agentId, AgentTypeId = typeId2 };

        var response = await client.PutAsJsonAsync($"/api/agents/{agentId}", updateDto);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AgentDto>();
        Assert.NotNull(updated);
        Assert.Equal(typeId2, updated.AgentTypeId);
        Assert.Equal(uniqueKey, updated.ApiKey);
    }

    [Fact]
    public async Task PutAgent_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.PutAsJsonAsync("/api/agents/1", new AgentDto { AgentTypeId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutAgent_Fail_WhenNotFound_ReturnsNotFound()
    {
        var client = await factory.CreateAuthenticatedClientAsync("put.notfound@exemple.fr");
        var response = await client.PutAsJsonAsync("/api/agents/999999", new AgentDto { AgentTypeId = 1 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutAgent_Fail_WhenNotOwner_ReturnsForbidden()
    {
        var ownerClient = await factory.CreateAuthenticatedClientAsync("put.owner@exemple.fr");
        var hackerClient = await factory.CreateAuthenticatedClientAsync("put.hacker@exemple.fr");
        int agentId, validTypeId;
        var uniqueKey = Guid.NewGuid().ToString("N");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.Users.FirstAsync(u => u.Email == "put.owner@exemple.fr");
            validTypeId = await db.AgentTypes.OrderBy(at => at.Id).Select(at => at.Id).FirstAsync();

            var agent = new Agent { AgentTypeId = validTypeId, UserId = owner.Id, ApiKey = uniqueKey };
            db.Agents.Add(agent);
            await db.SaveChangesAsync();
            agentId = agent.Id;
        }

        var response = await hackerClient.PutAsJsonAsync($"/api/agents/{agentId}", new AgentDto { AgentTypeId = validTypeId });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PutAgent_Fail_WithInvalidType_ReturnsBadRequest()
    {
        var email = "put.badtype@exemple.fr";
        var client = await factory.CreateAuthenticatedClientAsync(email);
        int agentId, validTypeId;
        var uniqueKey = Guid.NewGuid().ToString("N");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            validTypeId = await db.AgentTypes.OrderBy(at => at.Id).Select(at => at.Id).FirstAsync();

            var agent = new Agent { AgentTypeId = validTypeId, UserId = user.Id, ApiKey = uniqueKey };
            db.Agents.Add(agent);
            await db.SaveChangesAsync();
            agentId = agent.Id;
        }

        var response = await client.PutAsJsonAsync($"/api/agents/{agentId}", new AgentDto { AgentTypeId = 999999 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("agents.error.invalid_type", error?.Message);
    }

    // =========================================================================
    // 4. TESTS POUR DELETE /api/agents/{id}
    // =========================================================================

    [Fact]
    public async Task DeleteAgent_EndToEnd_RemovesAgentSuccessfully()
    {
        var email = "delete.e2e@exemple.fr";
        var client = await factory.CreateAuthenticatedClientAsync(email);
        int agentId;
        var uniqueKey = Guid.NewGuid().ToString("N");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var validTypeId = await db.AgentTypes.OrderBy(at => at.Id).Select(at => at.Id).FirstAsync();

            var agent = new Agent { AgentTypeId = validTypeId, UserId = user.Id, ApiKey = uniqueKey };
            db.Agents.Add(agent);
            await db.SaveChangesAsync();
            agentId = agent.Id;
        }

        var response = await client.DeleteAsync($"/api/agents/{agentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deleted = await db.Agents.FindAsync(agentId);
            Assert.Null(deleted);
        }
    }

    [Fact]
    public async Task DeleteAgent_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync("/api/agents/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAgent_Fail_WhenNotFound_ReturnsNotFound()
    {
        var client = await factory.CreateAuthenticatedClientAsync("delete.notfound@exemple.fr");
        var response = await client.DeleteAsync("/api/agents/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAgent_Fail_WhenNotOwner_ReturnsForbidden()
    {
        var ownerClient = await factory.CreateAuthenticatedClientAsync("delete.owner@exemple.fr");
        var hackerClient = await factory.CreateAuthenticatedClientAsync("delete.hacker@exemple.fr");
        int agentId;
        var uniqueKey = Guid.NewGuid().ToString("N");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.Users.FirstAsync(u => u.Email == "delete.owner@exemple.fr");
            var validTypeId = await db.AgentTypes.OrderBy(at => at.Id).Select(at => at.Id).FirstAsync();

            var agent = new Agent { AgentTypeId = validTypeId, UserId = owner.Id, ApiKey = uniqueKey };
            db.Agents.Add(agent);
            await db.SaveChangesAsync();
            agentId = agent.Id;
        }

        var response = await hackerClient.DeleteAsync($"/api/agents/{agentId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}