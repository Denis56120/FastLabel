namespace FastLabel.Backend.Endpoints;

public static class ProtocolEndpoints
{
    public static void MapProtocolEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/protocols").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, HttpContext httpContext) =>
        {
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

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