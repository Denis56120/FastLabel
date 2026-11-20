namespace FastLabel.Backend.Endpoints;

public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/templates").RequireAuthorization();

        group.MapGet("/", async (
            int page,
            int pageSize,
            string? sortBy,
            bool? sortAscending,
            string? filterByUser,
            string? filterByName,
            string? filterByStatus,
            AppDbContext db,
            ClaimsPrincipal claimsUser,
            HttpContext httpContext) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 500) pageSize = 500;

            var (languageId, language) = await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, userId);

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
                        .Where(tr => tr.Language.Code == language)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault() ?? t.BasicStatus.Name.DefaultTranslation)
                    .Contains(filterByStatus));
            }

            var naturalComparer = new NaturalStringHelpers();

            var result = await query.ToPagedResultAsync(
                page,
                pageSize,
                sortBy,
                sortAscending,
                t => new TemplateDto
                {
                    Id = t.Id,
                    Name = t.Name ?? string.Empty,
                    BasicStatusName = t.BasicStatus != null && t.BasicStatus.Name != null
                        ? (t.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == language)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? t.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    UserName = t.User != null ? t.User.Name : string.Empty,
                    JsonData = t.JsonData ?? string.Empty,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                },
                (items, sortField, ascending) => sortField?.ToLower() switch
                {
                    "username" => ascending ? items.OrderBy(x => x.UserName, naturalComparer) : items.OrderByDescending(x => x.UserName, naturalComparer),
                    "name" => ascending ? items.OrderBy(x => x.Name, naturalComparer) : items.OrderByDescending(x => x.Name, naturalComparer),
                    "basicstatusname" => ascending ? items.OrderBy(x => x.BasicStatusName, naturalComparer) : items.OrderByDescending(x => x.BasicStatusName, naturalComparer),
                    "createdat" => ascending ? items.OrderBy(x => x.CreatedAt) : items.OrderByDescending(x => x.CreatedAt),
                    "updatedat" => ascending ? items.OrderBy(x => x.UpdatedAt) : items.OrderByDescending(x => x.UpdatedAt),
                    _ => items.OrderByDescending(x => x.UpdatedAt)
                }
            );

            return Results.Ok(result);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal claimsUser, HttpContext httpContext) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var (languageId, language) = await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, userId);

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
                            .Where(tr => tr.Language.Code == language)
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
            if (!ValidationHelpers.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
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
            if (!ValidationHelpers.TryValidateModel(updatedDto, out var errorResult))
            {
                return errorResult!;
            }

            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var template = await DbHelpers.GetTemplateByIdAsync(db, id);
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
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var template = await DbHelpers.GetTemplateByIdAsync(db, id);
            if (template is null) return Results.NotFound(new ErrorResponseDto { Message = "template.error.not_found" });

            if (!isAdmin && template.UserId != userId)
            {
                return Results.Forbid();
            }

            db.Templates.Remove(template);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapPost("/delete-batch", async (List<int>? ids, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            if (ids == null || ids.Count == 0)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "common.error.no_ids_selected" });
            }

            var distinctIds = ids.Distinct().ToList();

            var query = db.Templates.Where(t => distinctIds.Contains(t.Id));
            if (!isAdmin)
            {
                query = query.Where(t => t.UserId == userId);
            }

            var templates = await query.ToListAsync();
            if (templates.Count == 0)
            {
                return Results.NotFound(new ErrorResponseDto { Message = "template.error.not_found" });
            }

            db.Templates.RemoveRange(templates);
            await db.SaveChangesAsync();

            return Results.Ok(new { deletedCount = templates.Count });
        });
    }
}