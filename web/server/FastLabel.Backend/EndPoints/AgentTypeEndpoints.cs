namespace FastLabel.Backend.Endpoints;

public static class AgentTypeEndpoints
{
    public static void MapAgentTypeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agenttypes").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal claimsUser, HttpContext httpContext) =>
        {
            var userIdString = ClaimsPrincipalHelpers.GetUserIdByClaimPrincipal(claimsUser);

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            var (languageId, language) = await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, userId);

            var agentTypes = await db.AgentTypes
                .Include(at => at.Name)
                    .ThenInclude(n => n.Translations)
                        .ThenInclude(tr => tr.Language)
                .OrderBy(at => at.Id)
                .Select(at => new AgentTypeDto
                {
                    Id = at.Id,
                    Name = at.Name != null
                        ? (at.Name.Translations
                            .Where(tr => tr.Language.Code == language)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? at.Name.DefaultTranslation)
                        : string.Empty
                })
                .ToListAsync();

            return Results.Ok(agentTypes);
        });
    }
}