namespace FastLabel.Backend.Endpoints;

public static class LanguageEndpoints
{
    public static void MapLanguageEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/languages");
        
        group.MapGet("/", async (AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            string lang = "en";

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

            // 2. Sinon (ou si non trouvé), on se replie sur le cookie du navigateur
            if (lang == "en" && httpContext.Request.Cookies.TryGetValue("language", out var cookieLang) && !string.IsNullOrEmpty(cookieLang))
            {
                lang = cookieLang;
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