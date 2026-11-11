using FastLabel.Backend.Data;
using FastLabel.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class LanguageEndpoints
{
    public static void MapLanguageEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/language");
        
        group.MapGet("/set", (string language, HttpResponse response) =>
        {
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