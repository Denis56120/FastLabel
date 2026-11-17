namespace FastLabel.Backend.Tests.Tests;

public class TemplateEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
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
    // 1. TESTS POUR GET /api/templates (Liste & Pagination)
    // =========================================================================

    [Fact]
    public async Task GetTemplates_EndToEnd_ReturnsPaginatedListForUser()
    {
        var email = "template.get@exemple.fr";
        var client = await CreateAuthenticatedClientAsync(email);
        var templateName = $"Template {Guid.NewGuid().ToString("N")[..6]}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var statusId = await db.BasicStatuses.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

            db.Templates.Add(new Template
            {
                UserId = user.Id,
                Name = templateName,
                JsonData = "{\"layout\": []}",
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/templates?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<TemplateDto>>();
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.Contains(result.Items, t => t.Name == templateName);
    }

    [Fact]
    public async Task GetTemplates_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/templates?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // 2. TESTS POUR GET /api/templates/{id}
    // =========================================================================

    [Fact]
    public async Task GetTemplateById_EndToEnd_ReturnsTemplateSuccessfully()
    {
        var email = "template.getbyid@exemple.fr";
        var client = await CreateAuthenticatedClientAsync(email);
        int templateId;
        var templateName = $"Template ID {Guid.NewGuid().ToString("N")[..6]}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var statusId = await db.BasicStatuses.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

            var template = new Template
            {
                UserId = user.Id,
                Name = templateName,
                JsonData = "{\"version\": 1}",
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Templates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.Id;
        }

        var response = await client.GetAsync($"/api/templates/{templateId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TemplateDto>();
        Assert.NotNull(result);
        Assert.Equal(templateId, result.Id);
        Assert.Equal(templateName, result.Name);
    }

    [Fact]
    public async Task GetTemplateById_Fail_WhenNotFound_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync("template.notfound@exemple.fr");
        var response = await client.GetAsync("/api/templates/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("template.error.not_found", error?.Message);
    }

    // =========================================================================
    // 3. TESTS POUR POST /api/templates
    // =========================================================================

    [Fact]
    public async Task PostTemplate_EndToEnd_CreatesTemplateSuccessfully()
    {
        var client = await CreateAuthenticatedClientAsync("template.post@exemple.fr");

        var dto = new TemplateDto
        {
            Name = "Nouveau Template ZPL",
            JsonData = "{\"width\": 100}"
        };

        var response = await client.PostAsJsonAsync("/api/templates", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TemplateDto>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("Nouveau Template ZPL", created.Name);
        Assert.Equal("{\"width\": 100}", created.JsonData);
    }

    [Fact]
    public async Task PostTemplate_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/templates", new TemplateDto { Name = "Fail", JsonData = "{}" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // 4. TESTS POUR PUT /api/templates/{id}
    // =========================================================================

    [Fact]
    public async Task PutTemplate_EndToEnd_UpdatesTemplateSuccessfully()
    {
        var email = "template.put@exemple.fr";
        var client = await CreateAuthenticatedClientAsync(email);
        int templateId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var statusId = await db.BasicStatuses.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

            var template = new Template
            {
                UserId = user.Id,
                Name = "Ancien Nom",
                JsonData = "{}",
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Templates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.Id;
        }

        var updateDto = new TemplateDto
        {
            Id = templateId,
            Name = "Nom Mis à Jour",
            JsonData = "{\"updated\": true}"
        };

        var response = await client.PutAsJsonAsync($"/api/templates/{templateId}", updateDto);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TemplateDto>();
        Assert.NotNull(updated);
        Assert.Equal("Nom Mis à Jour", updated.Name);
        Assert.Equal("{\"updated\": true}", updated.JsonData);
    }

    [Fact]
    public async Task PutTemplate_Fail_WhenNotOwner_ReturnsForbidden()
    {
        var ownerClient = await CreateAuthenticatedClientAsync("template.owner@exemple.fr");
        var hackerClient = await CreateAuthenticatedClientAsync("template.hacker@exemple.fr");
        int templateId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.Users.FirstAsync(u => u.Email == "template.owner@exemple.fr");
            var statusId = await db.BasicStatuses.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

            var template = new Template
            {
                UserId = owner.Id,
                Name = "Template Privé",
                JsonData = "{}",
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Templates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.Id;
        }

        var updateDto = new TemplateDto
        {
            Id = templateId,
            Name = "Hack",
            JsonData = "{}"
        };

        var response = await hackerClient.PutAsJsonAsync($"/api/templates/{templateId}", updateDto);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =========================================================================
    // 5. TESTS POUR DELETE /api/templates/{id}
    // =========================================================================

    [Fact]
    public async Task DeleteTemplate_EndToEnd_RemovesTemplateSuccessfully()
    {
        var email = "template.delete@exemple.fr";
        var client = await CreateAuthenticatedClientAsync(email);
        int templateId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var statusId = await db.BasicStatuses.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

            var template = new Template
            {
                UserId = user.Id,
                Name = "À Supprimer",
                JsonData = "{}",
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Templates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.Id;
        }

        var response = await client.DeleteAsync($"/api/templates/{templateId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deleted = await db.Templates.FindAsync(templateId);
            Assert.Null(deleted);
        }
    }

    [Fact]
    public async Task DeleteTemplate_Fail_WhenNotOwner_ReturnsForbidden()
    {
        var ownerClient = await CreateAuthenticatedClientAsync("template.delowner@exemple.fr");
        var hackerClient = await CreateAuthenticatedClientAsync("template.delhacker@exemple.fr");
        int templateId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.Users.FirstAsync(u => u.Email == "template.delowner@exemple.fr");
            var statusId = await db.BasicStatuses.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

            var template = new Template
            {
                UserId = owner.Id,
                Name = "Protégé",
                JsonData = "{}",
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Templates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.Id;
        }

        var response = await hackerClient.DeleteAsync($"/api/templates/{templateId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}