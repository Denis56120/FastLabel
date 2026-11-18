namespace FastLabel.Backend.Endpoints;

public static class LanguageEndpoints
{
    public static void MapLanguageEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/languages");
        
        group.MapGet("/", async (AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            string lang = HttpContextHelpers.GetLanguageCookie(httpContext);

            // 1. Si l'utilisateur est connecté, on cherche sa langue en base en priorité
            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier) 
                                    ?? claimsUser.FindFirstValue("nameid");

            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out var userId))
            {
                var userLang = await db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.Language != null ? u.Language.Code : null)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(userLang))
                {
                    lang = userLang;
                }
            }

            var languages = await db.Languages
                .Include(l => l.Description)
                    .ThenInclude(t => t.Translations)
                        .ThenInclude(tr => tr.Language)
                .Select(l => new LanguageDto
                {
                    Id = l.Id,
                    Code = l.Code,
                    Description = l.Description != null
                        ? (l.Description.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault()
                           ?? l.Description.DefaultTranslation)
                        : string.Empty,
                    Flag = l.Flag
                })
                .ToListAsync();

            return Results.Ok(languages);
        });

        group.MapGet("/set", async (string language, AppDbContext db, HttpContext httpContext) =>
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "language.error.invalid" });
            }

            // Vérifie dynamiquement dans la table Language si le code existe en base
            var languageExists = await db.Languages.AnyAsync(l => l.Code == language);
            if (!languageExists)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "language.error.invalid" });
            }

            // Pose le cookie proprement via l'extension HttpContext
            httpContext.SetLanguageCookie(language);

            return Results.Ok();
        });
    }
}