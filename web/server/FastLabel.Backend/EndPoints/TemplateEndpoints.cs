using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using FastLabel.Shared.DTOs;

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

        group.MapGet("/", async (AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var query = db.LabelTemplates
                .Include(t => t.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(t => t.Translations)
                            .ThenInclude(tr => tr.Language)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(t => t.UserId == userId);
            }

            var templates = await query
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
                    JsonData = t.JsonData ?? string.Empty,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                })
                .ToListAsync();

            return Results.Ok(templates);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var query = db.LabelTemplates
                .Include(t => t.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(t => t.Translations)
                            .ThenInclude(tr => tr.Language)
                .Where(t => t.Id == id)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(t => t.UserId == userId);
            }

            var template = await query
                .OrderBy(t => t.Id)
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
            var template = new LabelTemplate
            {
                UserId = userId,
                Name = dto.Name,
                JsonData = dto.JsonData,
                BasicStatusId = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.LabelTemplates.Add(template);
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

            var template = await db.LabelTemplates.FindAsync(id);
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

            var template = await db.LabelTemplates.FindAsync(id);
            if (template is null) return Results.NotFound(new ErrorResponseDto { Message = "template.error.not_found" });

            if (!isAdmin && template.UserId != userId)
            {
                return Results.Forbid();
            }

            db.LabelTemplates.Remove(template);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}