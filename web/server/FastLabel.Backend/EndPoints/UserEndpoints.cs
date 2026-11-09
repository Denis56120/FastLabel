using System.Security.Claims;
using FastLabel.Backend.Data;
using FastLabel.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users").RequireAuthorization();

        // 1. Récupérer le profil de l'utilisateur connecté
        group.MapGet("/me", async (AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return Results.NotFound(new { message = "User not found." });

            return Results.Ok(new
            {
                id = user.Id,
                name = user.Name,
                email = user.Email,
                isAdmin = user.IsAdmin,
                createdAt = user.CreatedAt
            });
        });

        // 2. Mettre à jour les préférences de langue
        group.MapPut("/me/preferences", async (UpdatePreferencesDto dto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            var user = await db.Users.FindAsync(userId);
            if (user == null)
                return Results.NotFound(new { message = "User not found." });

            var language = await db.PreferredLanguages
                .FirstOrDefaultAsync(l => l.Locale == dto.PreferredLanguage);

            if (language == null)
            {
                return Results.BadRequest(new { message = "Preferred language not found." });
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Preferences updated successfully.",
                preferredLanguage = language.Locale
            });
        });
    }
}