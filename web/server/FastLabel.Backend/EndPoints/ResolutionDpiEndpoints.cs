namespace FastLabel.Backend.Endpoints;

public static class ResolutionDpiEndpoints
{
    public static void MapResolutionDpiEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/resolutions").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db) =>
        {
            var resolutions = await db.ResolutionDpis
                .OrderBy(r => r.Value)
                .Select(r => new ResolutionDpiDto
                {
                    Id = r.Id,
                    Value = r.Value
                })
                .ToListAsync();

            return Results.Ok(resolutions);
        });
    }
}