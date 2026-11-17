namespace FastLabel.Backend.Endpoints;

public static class AntiforgeryEndpoints
{
    public static void MapAntiforgeryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/antiforgery/token", (IAntiforgery antiforgery, HttpContext context) =>
        {
            // Génère et stocke le token, et pose automatiquement le cookie XSRF-TOKEN
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        })
        .DisableAntiforgery(); // Indispensable pour ne pas exiger de token sur l'endpoint qui sert justement à le récupérer !
    }
}