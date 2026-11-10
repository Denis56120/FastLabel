using FastLabel.Backend.Data;
using FastLabel.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class TranslationEndpoints
{
    public static void MapTranslationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("api/translations/{language}", async (string language, AppDbContext db) =>
        {           
            var languageLower = language.ToLower();

            var translations = await db.Texts
                .Select(t => new TranslationDto
                {
                    Key = t.Tag,
                    Value = t.Translations
                        .Where(tr => tr.Language.Locale.ToLower() == languageLower)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault() ?? t.DefaultTranslation
                })
                .ToListAsync();

            return Results.Ok(translations);
        });
    }
}