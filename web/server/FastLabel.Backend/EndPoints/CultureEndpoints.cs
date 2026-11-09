using FastLabel.Backend.Data;
using FastLabel.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class CultureEndpoints
{
    public static void MapCultureEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/culture/set", (string culture, HttpResponse response) =>
        {
            response.Cookies.Append("preferred_language", culture, new CookieOptions
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