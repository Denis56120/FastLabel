namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class PrintJobEndpointsTests(CustomWebApplicationFactory factory)
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

    /// <summary>
    /// Helper pour récupérer des IDs valides pour lier une imprimante et un template à un PrintJob.
    /// </summary>
    private async Task<(int printerId, int templateId)> GetValidReferencesAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var protocolId = await db.Protocols.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        var resolutionId = await db.ResolutionDpis.OrderBy(r => r.Id).Select(r => r.Id).FirstAsync();
        var statusId = await db.BasicStatuses.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

        // Crée une imprimante pour l'utilisateur si elle n'existe pas
        var printer = new Printer
        {
            Name = "Imprimante Job Test",
            Location = "Test Lab",
            IP = $"192.168.2.{Guid.NewGuid().GetHashCode() % 255}",
            Port = 9100,
            ProtocolId = protocolId,
            ResolutionDpiId = resolutionId,
            BasicStatusId = statusId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        db.Printers.Add(printer);

        // Récupère ou crée un Template valide conforme au modèle Template.cs
        var template = await db.Templates.OrderBy(t => t.Id).FirstOrDefaultAsync();
        if (template == null)
        {
            template = new Template
            {
                Name = "Template Test",
                JsonData = "{\"version\": \"1.0\"}",
                UserId = userId,
                BasicStatusId = statusId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Templates.Add(template);
        }

        await db.SaveChangesAsync();
        return (printer.Id, template.Id);
    }

    // =========================================================================
    // 1. TESTS POUR GET /api/printjobs
    // =========================================================================

    [Fact]
    public async Task GetPrintJobs_EndToEnd_ReturnsPaginatedListForUser()
    {
        var email = "job.get@exemple.fr";
        var client = await CreateAuthenticatedClientAsync(email);
        int printJobId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var (printerId, templateId) = await GetValidReferencesAsync(user.Id);

            var job = new PrintJob
            {
                PrinterId = printerId,
                LabelTemplateId = templateId,
                Quantity = 5,
                InputDataJson = "{\"test\": true}",
                CreatedAt = DateTime.UtcNow
            };
            db.PrintJobs.Add(job);
            await db.SaveChangesAsync();
            printJobId = job.Id;
        }

        var response = await client.GetAsync("/api/printjobs?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<PrintJobsDto>>();
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.Contains(result.Items, j => j.Id == printJobId);
    }

    [Fact]
    public async Task GetPrintJobs_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/printjobs?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // 2. TESTS POUR GET /api/printjobs/{id}
    // =========================================================================

    [Fact]
    public async Task GetPrintJobById_EndToEnd_ReturnsJobSuccessfully()
    {
        var email = "job.getbyid@exemple.fr";
        var client = await CreateAuthenticatedClientAsync(email);
        int printJobId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var (printerId, templateId) = await GetValidReferencesAsync(user.Id);

            var job = new PrintJob
            {
                PrinterId = printerId,
                LabelTemplateId = templateId,
                Quantity = 2,
                InputDataJson = "{\"item\": \"A\"}",
                CreatedAt = DateTime.UtcNow
            };
            db.PrintJobs.Add(job);
            await db.SaveChangesAsync();
            printJobId = job.Id;
        }

        var response = await client.GetAsync($"/api/printjobs/{printJobId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PrintJobsDto>();
        Assert.NotNull(result);
        Assert.Equal(printJobId, result.Id);
        Assert.Equal(2, result.Quantity);
    }

    [Fact]
    public async Task GetPrintJobById_Fail_WhenNotFound_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync("job.notfound@exemple.fr");
        var response = await client.GetAsync("/api/printjobs/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("job.error.not_found", error?.Message);
    }
}