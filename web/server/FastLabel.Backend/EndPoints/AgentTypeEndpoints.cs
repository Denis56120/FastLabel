namespace FastLabel.Backend.Endpoints;

public static class AgentTypeEndpoints
{
    public static void MapAgentTypeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agenttypes").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier) 
                                ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            // Récupère la langue de l'utilisateur connecté en base
            var lang = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Language != null ? u.Language.Code : HttpContextExtensions.DefaultLanguage) // Utilise la langue par défaut si l'utilisateur n'a pas de langue définie
                .FirstOrDefaultAsync() ?? HttpContextExtensions.DefaultLanguage;

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
                            .Where(tr => tr.Language.Code == lang)
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