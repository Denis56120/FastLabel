using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using FastLabel.Backend.Data;
using FastLabel.Shared.DTOs;

namespace FastLabel.Backend.Endpoints;

public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/account/subscriptions", async (AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier) 
                                ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized(); 

            bool isAdmin = claimsUser.IsInRole("Admin");
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var query = db.Subscriptions
                .Include(s => s.Plan)
                    .ThenInclude(p => p.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Include(s => s.SubscriptionType)
                    .ThenInclude(st => st.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(s => s.UserId == userId);
            }

            var subscriptions = await query
                .OrderBy(s => s.Id)
                .Select(s => new SubscriptionDto
                {
                    Id = s.Id,
                    PlanName = s.Plan != null && s.Plan.Name != null
                        ? (s.Plan.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? s.Plan.Name.DefaultTranslation)
                        : string.Empty,
                    SubscriptionTypeName = s.SubscriptionType != null && s.SubscriptionType.Name != null
                        ? (s.SubscriptionType.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? s.SubscriptionType.Name.DefaultTranslation)
                        : string.Empty,
                    SubscriptionPeriod = s.SubscriptionPeriod,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(subscriptions);
        }).RequireAuthorization();
    }
}