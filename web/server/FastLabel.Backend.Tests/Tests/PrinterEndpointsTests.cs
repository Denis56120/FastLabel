namespace FastLabel.Backend.Tests.Tests;

[Collection("FastLabel Test Collection")]
public class PrinterEndpointsTests(TestWebApplicationFactory factory)
{
    private readonly TestWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateUnauthenticatedClient();

    /// <summary>
    /// Helper pour récupérer des IDs valides pour les clés étrangères (Protocol et ResolutionDpi).
    /// </summary>
    private async Task<(int protocolId, int resolutionId, int statusId)> GetValidReferencesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var protocolId = await db.Protocols.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        var resolutionId = await db.ResolutionDpis.OrderBy(r => r.Id).Select(r => r.Id).FirstAsync();
        var statusId = await db.BasicStatuses.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

        return (protocolId, resolutionId, statusId);
    }

    // =========================================================================
    // 1. TESTS POUR GET /api/printers (Liste & Pagination)
    // =========================================================================

    [Fact]
    public async Task GetPrinters_EndToEnd_ReturnsPaginatedListForUser()
    {
        var email = "printer.get@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(email);
        var (protocolId, resolutionId, statusId) = await GetValidReferencesAsync();
        var uniqueIp = $"192.168.1.{Guid.NewGuid().GetHashCode() % 255}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);

            db.Printers.Add(new Printer
            {
                Name = "Imprimante Test",
                Location = "Bureau A",
                IP = uniqueIp,
                Port = 9100,
                ProtocolId = protocolId,
                ResolutionDpiId = resolutionId,
                BasicStatusId = statusId,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/printers?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<PrinterDto>>();
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.Contains(result.Items, p => p.IP == uniqueIp);
    }

    [Fact]
    public async Task GetPrinters_Fail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/printers?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // 2. TESTS POUR GET /api/printers/{id}
    // =========================================================================

    [Fact]
    public async Task GetPrinterById_EndToEnd_ReturnsPrinterSuccessfully()
    {
        var email = "printer.getbyid@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(email);
        var (protocolId, resolutionId, statusId) = await GetValidReferencesAsync();
        int printerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);

            var printer = new Printer
            {
                Name = "Imprimante Unique",
                Location = "Atelier",
                IP = "10.0.0.50",
                Port = 9100,
                ProtocolId = protocolId,
                ResolutionDpiId = resolutionId,
                BasicStatusId = statusId,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            db.Printers.Add(printer);
            await db.SaveChangesAsync();
            printerId = printer.Id;
        }

        var response = await client.GetAsync($"/api/printers/{printerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PrinterDto>();
        Assert.NotNull(result);
        Assert.Equal(printerId, result.Id);
        Assert.Equal("Imprimante Unique", result.Name);
    }

    [Fact]
    public async Task GetPrinterById_Fail_WhenNotFound_ReturnsNotFound()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("printer.notfound@exemple.fr");
        var response = await client.GetAsync("/api/printers/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("printer.error.not_found", error?.Message);
    }

    // =========================================================================
    // 3. TESTS POUR POST /api/printers
    // =========================================================================

    [Fact]
    public async Task PostPrinter_EndToEnd_CreatesPrinterSuccessfully()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("printer.post@exemple.fr");
        var (protocolId, resolutionId, statusId) = await GetValidReferencesAsync();

        var dto = new PrinterDto
        {
            Name = "Nouvelle Imprimante",
            Location = "Accueil",
            IP = "192.168.1.200",
            Port = 9100,
            ProtocolId = protocolId,
            ResolutionDpiId = resolutionId,
            BasicStatusId = statusId
        };

        var response = await client.PostAsJsonAsync("/api/printers", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PrinterDto>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("Nouvelle Imprimante", created.Name);
    }

    [Fact]
    public async Task PostPrinter_Fail_WithInvalidProtocol_ReturnsBadRequest()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("printer.badproto@exemple.fr");
        var (_, resolutionId, statusId) = await GetValidReferencesAsync();

        var dto = new PrinterDto
        {
            Name = "Erreur Proto",
            Location = "Test",
            IP = "192.168.1.10",
            Port = 9100,
            ProtocolId = 99999, // Invalide
            ResolutionDpiId = resolutionId,
            BasicStatusId = statusId
        };

        var response = await client.PostAsJsonAsync("/api/printers", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("printer.error.invalid_protocol", error?.Message);
    }

    [Fact]
    public async Task PostPrinter_Fail_WithInvalidResolution_ReturnsBadRequest()
    {
        var client = await _factory.CreateAuthenticatedClientAsync("printer.badres@exemple.fr");
        var (protocolId, _, statusId) = await GetValidReferencesAsync();

        var dto = new PrinterDto
        {
            Name = "Erreur Res",
            Location = "Test",
            IP = "192.168.1.11",
            Port = 9100,
            ProtocolId = protocolId,
            ResolutionDpiId = 99999, // Invalide
            BasicStatusId = statusId
        };

        var response = await client.PostAsJsonAsync("/api/printers", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.Equal("printer.error.invalid_resolution", error?.Message);
    }

    // =========================================================================
    // 4. TESTS POUR PUT /api/printers/{id}
    // =========================================================================

    [Fact]
    public async Task PutPrinter_EndToEnd_UpdatesPrinterSuccessfully()
    {
        var email = "printer.put@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(email);
        var (protocolId, resolutionId, statusId) = await GetValidReferencesAsync();
        int printerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);

            var printer = new Printer
            {
                Name = "Ancien Nom",
                Location = "Ancien Lieu",
                IP = "192.168.1.50",
                Port = 9100,
                ProtocolId = protocolId,
                ResolutionDpiId = resolutionId,
                BasicStatusId = statusId,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            db.Printers.Add(printer);
            await db.SaveChangesAsync();
            printerId = printer.Id;
        }

        var updateDto = new PrinterDto
        {
            Id = printerId,
            Name = "Nom Mis à Jour",
            Location = "Nouveau Lieu",
            IP = "192.168.1.51",
            Port = 9100,
            ProtocolId = protocolId,
            ResolutionDpiId = resolutionId,
            BasicStatusId = statusId
        };

        var response = await client.PutAsJsonAsync($"/api/printers/{printerId}", updateDto);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<PrinterDto>();
        Assert.NotNull(updated);
        Assert.Equal("Nom Mis à Jour", updated.Name);
        Assert.Equal("Nouveau Lieu", updated.Location);
    }

    [Fact]
    public async Task PutPrinter_Fail_WhenNotOwner_ReturnsForbidden()
    {
        var ownerClient = await _factory.CreateAuthenticatedClientAsync("printer.owner@exemple.fr");
        var hackerClient = await _factory.CreateAuthenticatedClientAsync("printer.hacker@exemple.fr");
        var (protocolId, resolutionId, statusId) = await GetValidReferencesAsync();
        int printerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.Users.FirstAsync(u => u.Email == "printer.owner@exemple.fr");

            var printer = new Printer
            {
                Name = "Imprimante Privée",
                Location = "Bureau",
                IP = "192.168.1.99",
                Port = 9100,
                ProtocolId = protocolId,
                ResolutionDpiId = resolutionId,
                BasicStatusId = statusId,
                UserId = owner.Id,
                CreatedAt = DateTime.UtcNow
            };
            db.Printers.Add(printer);
            await db.SaveChangesAsync();
            printerId = printer.Id;
        }

        var updateDto = new PrinterDto
        {
            Id = printerId,
            Name = "Hack",
            Location = "Hack",
            IP = "192.168.1.99",
            Port = 9100,
            ProtocolId = protocolId,
            ResolutionDpiId = resolutionId,
            BasicStatusId = statusId
        };

        var response = await hackerClient.PutAsJsonAsync($"/api/printers/{printerId}", updateDto);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =========================================================================
    // 5. TESTS POUR DELETE /api/printers/{id}
    // =========================================================================

    [Fact]
    public async Task DeletePrinter_EndToEnd_RemovesPrinterSuccessfully()
    {
        var email = "printer.delete@exemple.fr";
        var client = await _factory.CreateAuthenticatedClientAsync(email);
        var (protocolId, resolutionId, statusId) = await GetValidReferencesAsync();
        int printerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);

            var printer = new Printer
            {
                Name = "À Supprimer",
                Location = "Poubelle",
                IP = "192.168.1.250",
                Port = 9100,
                ProtocolId = protocolId,
                ResolutionDpiId = resolutionId,
                BasicStatusId = statusId,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            db.Printers.Add(printer);
            await db.SaveChangesAsync();
            printerId = printer.Id;
        }

        var response = await client.DeleteAsync($"/api/printers/{printerId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deleted = await db.Printers.FindAsync(printerId);
            Assert.Null(deleted);
        }
    }

    [Fact]
    public async Task DeletePrinter_Fail_WhenNotOwner_ReturnsForbidden()
    {
        var ownerClient = await _factory.CreateAuthenticatedClientAsync("printer.delowner@exemple.fr");
        var hackerClient = await _factory.CreateAuthenticatedClientAsync("printer.delhacker@exemple.fr");
        var (protocolId, resolutionId, statusId) = await GetValidReferencesAsync();
        int printerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.Users.FirstAsync(u => u.Email == "printer.delowner@exemple.fr");

            var printer = new Printer
            {
                Name = "Protégée",
                Location = "Bureau",
                IP = "192.168.1.150",
                Port = 9100,
                ProtocolId = protocolId,
                ResolutionDpiId = resolutionId,
                BasicStatusId = statusId,
                UserId = owner.Id,
                CreatedAt = DateTime.UtcNow
            };
            db.Printers.Add(printer);
            await db.SaveChangesAsync();
            printerId = printer.Id;
        }

        var response = await hackerClient.DeleteAsync($"/api/printers/{printerId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}