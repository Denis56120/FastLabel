using FastLabel.Backend.Data;
using FastLabel.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class TranslationEndpoints
{
    public static void MapTranslationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("api/translations/{culture}", async (string culture, AppDbContext db) =>
        {
            Console.WriteLine($"Culture reçue : {culture}");
            
            var cultureLower = culture.ToLower();

            var translations = await db.Texts
                .Select(t => new TranslationDto
                {
                    Key = t.Tag,
                    Value = t.Translations
                        .Where(tr => tr.PreferredLanguage.Locale.ToLower() == cultureLower)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault() ?? t.DefaultTranslation
                })
                .ToListAsync();

            Console.WriteLine($"Traductions renvoyées : {translations.Count}");

            return Results.Ok(translations);
        });
    }
}