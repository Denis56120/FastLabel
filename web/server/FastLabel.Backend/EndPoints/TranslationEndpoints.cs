namespace FastLabel.Backend.Endpoints;

public static class TranslationEndpoints
{
    public static void MapTranslationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("api/translations/{language}", async (string language, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "language.error.invalid" });
            }

            var languageExists = await db.Languages.AnyAsync(l => l.Code == language);
            if (!languageExists)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "language.error.invalid" });
            }

            var translations = await db.Texts
                .Select(t => new TranslationDto
                {
                    Key = t.Tag,
                    Value = t.Translations
                        .Where(tr => tr.Language.Code == language)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault() ?? t.DefaultTranslation
                })
                .ToListAsync();

            return Results.Ok(translations);
        });
    }
}