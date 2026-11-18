namespace FastLabel.Backend.Endpoints;

public static class LanguageEndpoints
{
    public static void MapLanguageEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/languages");
        
        group.MapGet("/", async (AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var (languageId, language) = await DbHelpers.GetUserLanguageByUserClaimAsync(httpContext, db, claimsUser);

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
                            .Where(tr => tr.Language.Code == language)
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

            var lang = await DbHelpers.GetLanguageByCodeAsync(db, language);  
            if (lang == null)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "language.error.invalid" });
            }

            httpContext.SetLanguageCookie(language);

            return Results.Ok();
        });
    }
}