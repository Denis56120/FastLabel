namespace FastLabel.Backend.Endpoints;

public static class LanguageEndpoints
{
    public static void MapLanguageEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/languages");
        
        group.MapGet("/", async (AppDbContext db, HttpContext httpContext) =>
        {
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

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
                        : string.Empty
                })
                .ToListAsync();

            return Results.Ok(languages);
        });

        group.MapGet("/set", (string language, HttpResponse response) =>
        {
            if (string.IsNullOrWhiteSpace(language) || (language != "fr" && language != "en"))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "language.error.invalid" });
            }

            response.Cookies.Append("language", language, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false
            });

            return Results.Ok();
        });
    }
}