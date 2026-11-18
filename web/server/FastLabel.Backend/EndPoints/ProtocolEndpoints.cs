namespace FastLabel.Backend.Endpoints;

public static class ProtocolEndpoints
{
    public static void MapProtocolEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/protocols").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal claimsUser, HttpContext httpContext) =>
        {
            var userIdString = ClaimsPrincipalHelpers.GetUserIdByClaimPrincipal(claimsUser);

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            var (languageId, language) = await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, userId);

            var protocols = await db.Protocols
                .Include(p => p.Name)
                    .ThenInclude(n => n.Translations)
                        .ThenInclude(tr => tr.Language)
                .Select(p => new ProtocolDto
                {
                    Id = p.Id,
                    Name = p.Name != null && p.Name.Translations != null
                        ? (p.Name.Translations
                            .Where(tr => tr.Language.Code == language)
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