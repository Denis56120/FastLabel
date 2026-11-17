namespace FastLabel.Backend.Endpoints;

public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/templates").RequireAuthorization();

        static (int userId, bool isAdmin) GetUserInfo(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? user.FindFirst("nameid")?.Value 
                           ?? user.FindFirst("unique_name")?.Value;

            int.TryParse(userIdClaim, out var userId);
            bool isAdmin = user.IsInRole("Admin");

            return (userId, isAdmin);
        }

        group.MapGet("/", async (
            int page, 
            int pageSize, 
            string? sortBy, 
            bool? sortAscending, 
            string? filterByUser,
            string? filterByName,
            string? filterByStatus,
            AppDbContext db, 
            ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);
            if (userId == 0) return Results.Unauthorized();

            // Valeurs par défaut de sécurité
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 500) pageSize = 500;

            // Récupère la langue de l'utilisateur connecté pour traduire les statuts
            var lang = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Language != null ? u.Language.Code : HttpContextExtensions.DefaultLanguage)
                .FirstOrDefaultAsync() ?? HttpContextExtensions.DefaultLanguage;

            var query = db.Templates
                .Include(t => t.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(t => t.Translations)
                            .ThenInclude(tr => tr.Language)
                .Include(t => t.User)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(t => t.UserId == userId);
            }

            // 1. Application des filtres textuels reçus du front
            if (!string.IsNullOrWhiteSpace(filterByUser))
            {
                query = query.Where(t => t.User != null && t.User.Name.Contains(filterByUser));
            }

            if (!string.IsNullOrWhiteSpace(filterByName))
            {
                query = query.Where(t => t.Name != null && t.Name.Contains(filterByName));
            }

            if (!string.IsNullOrWhiteSpace(filterByStatus))
            {
                query = query.Where(t => t.BasicStatus != null && t.BasicStatus.Name != null &&
                    (t.BasicStatus.Name.Translations
                        .Where(tr => tr.Language.Code == lang)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault() ?? t.BasicStatus.Name.DefaultTranslation)
                    .Contains(filterByStatus));
            }

            // 2. Calcul du nombre total d'éléments après filtrage et avant pagination
            var totalCount = await query.CountAsync();

            // 3. Application du tri dynamique (basé sur les en-têtes cliqués)
            bool ascending = sortAscending ?? true;
            query = sortBy?.ToLower() switch
            {
                "username" => ascending ? query.OrderBy(t => t.User != null ? t.User.Name : string.Empty) : query.OrderByDescending(t => t.User != null ? t.User.Name : string.Empty),
                "name" => ascending ? query.OrderBy(t => t.Name) : query.OrderByDescending(t => t.Name),
                "basicstatusname" => ascending ? query.OrderBy(t => t.BasicStatus.Name.DefaultTranslation) : query.OrderByDescending(t => t.BasicStatus.Name.DefaultTranslation),
                "createdat" => ascending ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt),
                "updatedat" => ascending ? query.OrderBy(t => t.UpdatedAt) : query.OrderByDescending(t => t.UpdatedAt),
                _ => query.OrderByDescending(t => t.UpdatedAt) // Tri par défaut
            };

            // 4. Application de la pagination (Skip & Take)
            var templates = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TemplateDto
                {
                    Id = t.Id,
                    Name = t.Name ?? string.Empty,
                    BasicStatusName = t.BasicStatus != null && t.BasicStatus.Name != null 
                        ? (t.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault() 
                           ?? t.BasicStatus.Name.DefaultTranslation) 
                        : string.Empty,
                    UserName = t.User != null ? t.User.Name : string.Empty,
                    JsonData = t.JsonData ?? string.Empty,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                })
                .ToListAsync();

            // 5. Encapsulation dans le PagedResultDto
            var result = new PagedResultDto<TemplateDto>
            {
                Items = templates,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Results.Ok(result);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);
            if (userId == 0) return Results.Unauthorized();

            // Récupère la langue de l'utilisateur connecté pour traduire les statuts
            var lang = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Language != null ? u.Language.Code : HttpContextExtensions.DefaultLanguage)
                .FirstOrDefaultAsync() ?? HttpContextExtensions.DefaultLanguage;

            var query = db.Templates
                .Include(t => t.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(t => t.Translations)
                            .ThenInclude(tr => tr.Language)
                .Include(t => t.User)
                .Where(t => t.Id == id)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(t => t.UserId == userId);
            }

            var template = await query
                .Select(t => new TemplateDto
                {
                    Id = t.Id,
                    Name = t.Name ?? string.Empty,
                    BasicStatusName = t.BasicStatus != null && t.BasicStatus.Name != null 
                        ? (t.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault() 
                           ?? t.BasicStatus.Name.DefaultTranslation) 
                        : string.Empty,
                    UserName = t.User != null ? t.User.Name : string.Empty,
                    JsonData = t.JsonData ?? string.Empty,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return template is not null 
                ? Results.Ok(template) 
                : Results.NotFound(new ErrorResponseDto { Message = "template.error.not_found" });
        });

        group.MapPost("/", async (TemplateDto dto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            if (!ValidationExtensions.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            var (userId, _) = GetUserInfo(claimsUser);
            if (userId == 0) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var template = new Template
            {
                UserId = userId,
                Name = dto.Name,
                JsonData = dto.JsonData,
                BasicStatusId = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Templates.Add(template);
            await db.SaveChangesAsync();

            dto.Id = template.Id;
            dto.CreatedAt = template.CreatedAt;
            dto.UpdatedAt = template.UpdatedAt;
            return Results.Created($"/api/templates/{template.Id}", dto);
        });

        group.MapPut("/{id:int}", async (int id, TemplateDto updatedDto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            if (!ValidationExtensions.TryValidateModel(updatedDto, out var errorResult))
            {
                return errorResult!;
            }

            var (userId, isAdmin) = GetUserInfo(claimsUser);
            if (userId == 0) return Results.Unauthorized();

            var template = await db.Templates.FindAsync(id);
            if (template is null) return Results.NotFound(new ErrorResponseDto { Message = "template.error.not_found" });

            if (!isAdmin && template.UserId != userId)
            {
                return Results.Forbid();
            }

            template.Name = updatedDto.Name;
            template.JsonData = updatedDto.JsonData;
            template.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            
            updatedDto.CreatedAt = template.CreatedAt;
            updatedDto.UpdatedAt = template.UpdatedAt;
            return Results.Ok(updatedDto);
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);
            if (userId == 0) return Results.Unauthorized();

            var template = await db.Templates.FindAsync(id);
            if (template is null) return Results.NotFound(new ErrorResponseDto { Message = "template.error.not_found" });

            if (!isAdmin && template.UserId != userId)
            {
                return Results.Forbid();
            }

            db.Templates.Remove(template);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}