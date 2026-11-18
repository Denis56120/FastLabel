namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class ResellerEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly TestWebApplicationFactory _factory = factory;

    // =========================================================================
    // 1. TESTS POUR GET /api/reseller/clients
    // =========================================================================

    [Fact]
    public async Task GetResellerClients_EndToEnd_ReturnsClientsListForReseller()
    {
        var resellerEmail = "reseller.get@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(resellerEmail, isReseller: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var reseller = await db.Users.FirstAsync(u => u.Email == resellerEmail);
            var inactiveStatus = await db.BasicStatuses.OrderBy(s => s.Id).FirstAsync();

            db.Users.Add(new User
            {
                Name = "Client Final 1",
                Email = "client1@exemple.fr",
                PasswordHash = "hash",
                BasicStatusId = inactiveStatus.Id,
                ResellerId = reseller.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/reseller/clients?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<UserDto>>();
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.Contains(result.Items, u => u.Email == "client1@exemple.fr");
    }

    [Fact]
    public async Task GetResellerClients_Fail_WhenRegularUser_ReturnsForbidden()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("regular.user@exemple.fr", isReseller: false);

        var response = await client.GetAsync("/api/reseller/clients");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetResellerClients_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateUnauthenticatedClient();
        var response = await client.GetAsync("/api/reseller/clients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // 2. TESTS POUR POST /api/reseller/clients
    // =========================================================================

    [Fact]
    public async Task PostResellerClient_EndToEnd_CreatesClientSuccessfully()
    {
        var resellerEmail = "reseller.post@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(resellerEmail, isReseller: true);

        var newClientDto = new UserDto
        {
            Name = "Nouveau Client",
            Email = "nouveau.client@exemple.fr"
        };

        var response = await client.PostAsJsonAsync("/api/reseller/clients", newClientDto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("nouveau.client@exemple.fr", created.Email);
    }

    [Fact]
    public async Task PostResellerClient_Fail_WhenEmailAlreadyTaken_ReturnsBadRequest()
    {
        var resellerEmail = "reseller.postfail@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(resellerEmail, isReseller: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var inactiveStatus = await db.BasicStatuses.OrderBy(s => s.Id).FirstAsync();

            db.Users.Add(new User
            {
                Name = "Existing",
                Email = "existing@exemple.fr",
                PasswordHash = "hash",
                BasicStatusId = inactiveStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            });
            await db.SaveChangesAsync();
        }

        var dto = new UserDto
        {
            Name = "Autre",
            Email = "existing@exemple.fr"
        };

        var response = await client.PostAsJsonAsync("/api/reseller/clients", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("user.error.email_already_taken", error?.Message);
    }

    [Fact]
    public async Task PostResellerClient_Fail_WhenRegularUser_ReturnsForbidden()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("regular.post@exemple.fr", isReseller: false);

        var dto = new UserDto
        {
            Name = "Client",
            Email = "client@exemple.fr"
        };

        var response = await client.PostAsJsonAsync("/api/reseller/clients", dto);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =========================================================================
    // 3. TESTS POUR DELETE /api/reseller/clients/{id}
    // =========================================================================

    [Fact]
    public async Task DeleteResellerClient_EndToEnd_RemovesClientSuccessfully()
    {
        var resellerEmail = "reseller.delete@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(resellerEmail, isReseller: true);
        int clientId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var reseller = await db.Users.OrderBy(u => u.Id).FirstAsync(u => u.Email == resellerEmail);
            var inactiveStatus = await db.BasicStatuses.OrderBy(s => s.Id).FirstAsync();

            var clientUser = new User
            {
                Name = "Client To Delete",
                Email = "todelete@exemple.fr",
                PasswordHash = "hash",
                BasicStatusId = inactiveStatus.Id,
                ResellerId = reseller.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = 1
            };
            db.Users.Add(clientUser);
            await db.SaveChangesAsync();
            clientId = clientUser.Id;
        }

        var response = await client.DeleteAsync($"/api/reseller/clients/{clientId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deleted = await db.Users.FindAsync(clientId);
            Assert.Null(deleted);
        }
    }

    [Fact]
    public async Task DeleteResellerClient_Fail_WhenNotFoundOrNotOwner_ReturnsNotFound()
    {
        var resellerEmail = "reseller.delfail@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(resellerEmail, isReseller: true);

        var response = await client.DeleteAsync("/api/reseller/clients/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("user.error.not_found", error?.Message);
    }

    [Fact]
    public async Task DeleteResellerClient_Fail_WhenRegularUser_ReturnsForbidden()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("regular.delete@exemple.fr", isReseller: false);

        var response = await client.DeleteAsync("/api/reseller/clients/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}