namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class UserEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly TestWebApplicationFactory _factory = factory;

    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

    private class FakeEmailService : IEmailService
    {
        public Task SendEmailAsync(string email, string subject, string message) => Task.CompletedTask;
    }

    // =========================================================================
    // 1. TESTS POUR GET /api/users/me
    // =========================================================================

    [Fact]
    public async Task GetCurrentUser_EndToEnd_ReturnsUserProfileSuccessfully()
    {
        var email = "user.me@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(email);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
    }

    [Fact]
    public async Task GetCurrentUser_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // 2. TESTS POUR PUT /api/users/me
    // =========================================================================

    [Fact]
    public async Task UpdateCurrentUser_EndToEnd_UpdatesProfileSuccessfully()
    {
        var email = "user.update.me@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(email);
        int langId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            langId = await db.Languages.OrderBy(l => l.Id).Select(l => l.Id).FirstAsync();
        }

        var updateDto = new UserDto
        {
            Name = "Nouveau Nom",
            Email = email,
            LanguageId = langId
        };

        var response = await client.PutAsJsonAsync("/api/users/me", updateDto);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateCurrentUser_Fail_WhenEmailAlreadyTaken_ReturnsBadRequest()
    {
        var email1 = "user.one@exemple.fr";
        var email2 = "user.two@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(email1);
        int langId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            langId = await db.Languages.OrderBy(l => l.Id).Select(l => l.Id).FirstAsync();
            var activeStatus = await db.BasicStatuses.OrderBy(s => s.Id).FirstAsync();

            db.Users.Add(new User
            {
                Name = "Other",
                Email = email2,
                PasswordHash = "hash",
                BasicStatusId = activeStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = langId
            });
            await db.SaveChangesAsync();
        }

        var updateDto = new UserDto
        {
            Name = "Nom Test",
            Email = email2, // Déjà pris par un autre utilisateur
            LanguageId = langId
        };

        var response = await client.PutAsJsonAsync("/api/users/me", updateDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("user.error.email_already_taken", error?.Message);
    }

    // =========================================================================
    // 3. TESTS ADMIN : GET /api/users (Liste Admin)
    // =========================================================================

    [Fact]
    public async Task AdminGetUsers_EndToEnd_ReturnsPaginatedListForAdmin()
    {
        var adminEmail = "admin.get@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(adminEmail, isAdmin: true);

        var response = await client.GetAsync("/api/users?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<UserDto>>();
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
    }

    [Fact]
    public async Task AdminGetUsers_Fail_WhenRegularUser_ReturnsForbiddenOrUnauthorized()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("regular.getusers@exemple.fr", isAdmin: false);

        var response = await client.GetAsync("/api/users");

        // Selon la politique de rôle (AdminOnly), retourne Forbidden (403) ou Unauthorized (401)
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // 4. TESTS ADMIN : POST /api/users (Création Admin)
    // =========================================================================

    [Fact]
    public async Task AdminPostUser_EndToEnd_CreatesUserSuccessfully()
    {
        var adminEmail = "admin.post@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(adminEmail, isAdmin: true);
        int langId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            langId = await db.Languages.OrderBy(l => l.Id).Select(l => l.Id).FirstAsync();
        }

        var newDto = new UserDto
        {
            Name = "Créé par Admin",
            Email = "createdbyadmin@exemple.fr",
            LanguageId = langId,
            Password = "ValidPassword123!"
        };

        var response = await client.PostAsJsonAsync("/api/users", newDto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("createdbyadmin@exemple.fr", created.Email);
    }

    [Fact]
    public async Task AdminPostUser_Fail_WhenLanguageNotFound_ReturnsBadRequest()
    {
        var adminEmail = "admin.postfail@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(adminEmail, isAdmin: true);

        var newDto = new UserDto
        {
            Name = "Lang Invalide",
            Email = "invalidlang@exemple.fr",
            LanguageId = 99999, // Inexistant
            Password = "ValidPassword123!"
        };

        var response = await client.PostAsJsonAsync("/api/users", newDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("user.error.language_not_found", error?.Message);
    }

    // =========================================================================
    // 5. TESTS ADMIN : DELETE /api/users/{id}
    // =========================================================================

    [Fact]
    public async Task AdminDeleteUser_EndToEnd_RemovesUserSuccessfully()
    {
        var adminEmail = "admin.delete@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(adminEmail, isAdmin: true);
        int targetUserId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var activeStatus = await db.BasicStatuses.OrderBy(s => s.Id).FirstAsync();
            var langId = await db.Languages.OrderBy(l => l.Id).Select(l => l.Id).FirstAsync();

            var target = new User
            {
                Name = "À Supprimer",
                Email = "todelete.admin@exemple.fr",
                PasswordHash = "hash",
                BasicStatusId = activeStatus.Id,
                CreatedAt = DateTime.UtcNow,
                LanguageId = langId
            };
            db.Users.Add(target);
            await db.SaveChangesAsync();
            targetUserId = target.Id;
        }

        var response = await client.DeleteAsync($"/api/users/{targetUserId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deleted = await db.Users.FindAsync(targetUserId);
            Assert.Null(deleted);
        }
    }
}