namespace FastLabel.Backend.Endpoints;

public static class PrinterEndpoints
{
    public static void MapPrinterEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/printers").RequireAuthorization();

        group.MapGet("/", async (
            int page, 
            int pageSize, 
            string? sortBy, 
            bool? sortAscending, 
            string? filterByUser,
            string? filterByName,
            string? filterByLocation,
            AppDbContext db, 
            ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            // Valeurs par défaut de sécurité si non fournies
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            // Récupère la langue de l'utilisateur connecté en base
            var lang = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Language != null ? u.Language.Code : HttpContextExtensions.DefaultLanguage)
                .FirstOrDefaultAsync() ?? HttpContextExtensions.DefaultLanguage;

            var query = db.Printers
                .Include(p => p.Protocol)
                    .ThenInclude(pr => pr.Name)
                        .ThenInclude(t => t.Translations)
                            .ThenInclude(tr => tr.Language)
                .Include(p => p.ResolutionDpi)
                .Include(p => p.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(t => t.Translations)
                            .ThenInclude(tr => tr.Language)
                .Include(p => p.User)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(p => p.UserId == userId);
            }

            // 1. Application des filtres textuels reçus du front
            if (!string.IsNullOrWhiteSpace(filterByUser))
            {
                query = query.Where(p => p.User != null && p.User.Name.Contains(filterByUser));
            }

            if (!string.IsNullOrWhiteSpace(filterByName))
            {
                query = query.Where(p => p.Name != null && p.Name.Contains(filterByName));
            }

            if (!string.IsNullOrWhiteSpace(filterByLocation))
            {
                query = query.Where(p => p.Location != null && p.Location.Contains(filterByLocation));
            }

            var naturalComparer = new NaturalStringComparer();

            // 2. Utilisation de l'extension ToPagedResultAsync pour la pagination et le tri naturel
            var result = await query.ToPagedResultAsync(
                page,
                pageSize,
                sortBy,
                sortAscending,
                p => new PrinterDto
                {
                    Id = p.Id,
                    Name = p.Name ?? string.Empty,
                    Location = p.Location ?? string.Empty,
                    IP = p.IP ?? string.Empty,
                    Port = p.Port,
                    ProtocolId = p.ProtocolId,
                    ProtocolName = p.Protocol != null && p.Protocol.Name != null
                        ? (p.Protocol.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? p.Protocol.Name.DefaultTranslation)
                        : string.Empty,
                    ResolutionDpiId = p.ResolutionDpiId,
                    ResolutionDpiName = p.ResolutionDpi != null ? $"{p.ResolutionDpi.Value} DPI" : string.Empty,
                    BasicStatusId = p.BasicStatusId,
                    StatusName = p.BasicStatus != null && p.BasicStatus.Name != null 
                        ? (p.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault() 
                           ?? p.BasicStatus.Name.DefaultTranslation) 
                        : string.Empty,
                    UserName = p.User != null ? p.User.Name : string.Empty,
                    CreatedAt = p.CreatedAt
                },
                (items, sortField, ascending) => sortField?.ToLower() switch
                {
                    "username" => ascending ? items.OrderBy(x => x.UserName, naturalComparer) : items.OrderByDescending(x => x.UserName, naturalComparer),
                    "name" => ascending ? items.OrderBy(x => x.Name, naturalComparer) : items.OrderByDescending(x => x.Name, naturalComparer),
                    "location" => ascending ? items.OrderBy(x => x.Location, naturalComparer) : items.OrderByDescending(x => x.Location, naturalComparer),
                    "protocolname" => ascending ? items.OrderBy(x => x.ProtocolName, naturalComparer) : items.OrderByDescending(x => x.ProtocolName, naturalComparer),
                    "resolutiondpivalue" => ascending ? items.OrderBy(x => x.ResolutionDpiName) : items.OrderByDescending(x => x.ResolutionDpiName),
                    "statusname" => ascending ? items.OrderBy(x => x.StatusName, naturalComparer) : items.OrderByDescending(x => x.StatusName, naturalComparer),
                    "createdat" => ascending ? items.OrderBy(x => x.CreatedAt) : items.OrderByDescending(x => x.CreatedAt),
                    _ => items.OrderBy(x => x.Id)
                }
            );

            return Results.Ok(result);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            // Récupère la langue de l'utilisateur connecté en base
            var lang = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Language != null ? u.Language.Code : HttpContextExtensions.DefaultLanguage)
                .FirstOrDefaultAsync() ?? HttpContextExtensions.DefaultLanguage;

            var query = db.Printers
                .Include(p => p.Protocol)
                    .ThenInclude(pr => pr.Name)
                        .ThenInclude(t => t.Translations)
                            .ThenInclude(tr => tr.Language)
                .Include(p => p.ResolutionDpi)
                .Include(p => p.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(t => t.Translations)
                            .ThenInclude(tr => tr.Language)
                .Include(p => p.User)
                .Where(p => p.Id == id)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(p => p.UserId == userId);
            }

            var printer = await query
                .Select(p => new PrinterDto
                {
                    Id = p.Id,
                    Name = p.Name ?? string.Empty,
                    Location = p.Location ?? string.Empty,
                    IP = p.IP ?? string.Empty,
                    Port = p.Port,
                    ProtocolId = p.ProtocolId,
                    ProtocolName = p.Protocol != null && p.Protocol.Name != null
                        ? (p.Protocol.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? p.Protocol.Name.DefaultTranslation)
                        : string.Empty,
                    ResolutionDpiId = p.ResolutionDpiId,
                    ResolutionDpiName = p.ResolutionDpi != null ? $"{p.ResolutionDpi.Value} DPI" : string.Empty,
                    BasicStatusId = p.BasicStatusId,
                    StatusName = p.BasicStatus != null && p.BasicStatus.Name != null 
                        ? (p.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault() 
                           ?? p.BasicStatus.Name.DefaultTranslation) 
                        : string.Empty,
                    UserName = p.User != null ? p.User.Name : string.Empty,
                    CreatedAt = p.CreatedAt
                })
                .FirstOrDefaultAsync();

            return printer is not null 
                ? Results.Ok(printer) 
                : Results.NotFound(new ErrorResponseDto { Message = "printer.error.not_found" });
        });

        group.MapPost("/", async (PrinterDto dto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            if (!ValidationExtensions.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var protocolExists = await db.Protocols.AnyAsync(p => p.Id == dto.ProtocolId);
            if (!protocolExists) return Results.BadRequest(new ErrorResponseDto { Message = "printer.error.invalid_protocol" });

            var resolutionExists = await db.ResolutionDpis.AnyAsync(r => r.Id == dto.ResolutionDpiId);
            if (!resolutionExists) return Results.BadRequest(new ErrorResponseDto { Message = "printer.error.invalid_resolution" });

            var printer = new Printer
            {
                UserId = userId,
                Name = dto.Name,
                Location = dto.Location,
                IP = dto.IP,
                Port = dto.Port,
                ProtocolId = dto.ProtocolId,
                ResolutionDpiId = dto.ResolutionDpiId,
                BasicStatusId = dto.BasicStatusId > 0 ? dto.BasicStatusId : 1,
                CreatedAt = DateTime.UtcNow
            };

            db.Printers.Add(printer);
            await db.SaveChangesAsync();

            dto.Id = printer.Id;
            dto.CreatedAt = printer.CreatedAt;
            return Results.Created($"/api/printers/{printer.Id}", dto);
        });

        group.MapPut("/{id:int}", async (int id, PrinterDto updatedDto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            if (!ValidationExtensions.TryValidateModel(updatedDto, out var errorResult))
            {
                return errorResult!;
            }

            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var printer = await db.Printers.FindAsync(id);
            if (printer is null) return Results.NotFound(new ErrorResponseDto { Message = "printer.error.not_found" });

            if (!isAdmin && printer.UserId != userId)
            {
                return Results.Forbid();
            }

            var protocolExists = await db.Protocols.AnyAsync(p => p.Id == updatedDto.ProtocolId);
            if (!protocolExists) return Results.BadRequest(new ErrorResponseDto { Message = "printer.error.invalid_protocol" });

            var resolutionExists = await db.ResolutionDpis.AnyAsync(r => r.Id == updatedDto.ResolutionDpiId);
            if (!resolutionExists) return Results.BadRequest(new ErrorResponseDto { Message = "printer.error.invalid_resolution" });

            printer.Name = updatedDto.Name;
            printer.Location = updatedDto.Location;
            printer.IP = updatedDto.IP;
            printer.Port = updatedDto.Port;
            printer.ProtocolId = updatedDto.ProtocolId;
            printer.ResolutionDpiId = updatedDto.ResolutionDpiId;

            await db.SaveChangesAsync();
            return Results.Ok(updatedDto);
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var printer = await db.Printers.FindAsync(id);
            if (printer is null) return Results.NotFound(new ErrorResponseDto { Message = "printer.error.not_found" });

            if (!isAdmin && printer.UserId != userId)
            {
                return Results.Forbid();
            }

            db.Printers.Remove(printer);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}