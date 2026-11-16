using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using FastLabel.Shared.DTOs;

namespace FastLabel.Backend.Endpoints;

public static class ProtocolEndpoints
{
    public static void MapProtocolEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/protocols").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier) 
                                ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            // Récupère la langue de l'utilisateur connecté en base
            var lang = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Language != null ? u.Language.Code : "en")
                .FirstOrDefaultAsync() ?? "en";

            var protocols = await db.Protocols
                .Include(p => p.Name)
                    .ThenInclude(n => n.Translations)
                        .ThenInclude(tr => tr.Language)
                .Select(p => new ProtocolDto
                {
                    Id = p.Id,
                    Name = p.Name != null && p.Name.Translations != null
                        ? (p.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? p.Name.DefaultTranslation)
                        : string.Empty
                })
                .ToListAsync();

            return Results.Ok(protocols);
        });
    }
}