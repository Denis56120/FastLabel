using System.Security.Claims;
using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using FastLabel.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/users/me", async (AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier) 
                                ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var userDto = await db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Where(u => u.Id == userId)
                .OrderBy(u => u.Id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? u.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (userDto == null)
                return Results.NotFound(new { message = "User not found." });

            return Results.Ok(userDto);
        }).RequireAuthorization();

        var adminGroup = routes.MapGroup("/api/users").RequireAuthorization("AdminOnly");

        adminGroup.MapGet("/", async (AppDbContext db, HttpContext httpContext) =>
        {
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var users = await db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? u.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(users);
        });

        adminGroup.MapGet("/{id:int}", async (int id, AppDbContext db, HttpContext httpContext) =>
        {
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var dto = await db.Users
                .Include(u => u.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Where(u => u.Id == id)
                .OrderBy(u => u.Id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    BasicStatusName = u.BasicStatus != null && u.BasicStatus.Name != null
                        ? (u.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? u.BasicStatus.Name.DefaultTranslation)
                        : string.Empty,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (dto == null) return Results.NotFound();

            return Results.Ok(dto);
        });

        adminGroup.MapPost("/", async (UserDto dto, AppDbContext db) =>
        {
            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                IsAdmin = dto.IsAdmin,
                BasicStatusId = 1,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            dto.Id = user.Id;
            return Results.Created($"/api/users/{user.Id}", dto);
        });

        adminGroup.MapPut("/{id:int}", async (int id, UserDto updatedDto, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound();

            user.Name = updatedDto.Name;
            user.Email = updatedDto.Email;
            user.IsAdmin = updatedDto.IsAdmin;

            await db.SaveChangesAsync();
            return Results.Ok(user);
        });

        adminGroup.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound();

            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}