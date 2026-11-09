using FastLabel.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class PrintJobEndpoints
{
    public static void MapPrintJobEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/jobs");

        group.MapGet("/", async (AppDbContext db, int? limit) =>
        {
            var query = db.PrintJobs
                .Include(j => j.Printer)
                .OrderByDescending(j => j.CreatedAt);

            var jobs = limit.HasValue 
                ? await query.Take(limit.Value).ToListAsync() 
                : await query.Take(100).ToListAsync();

            return Results.Ok(jobs);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var job = await db.PrintJobs
                .Include(j => j.Printer)
                .FirstOrDefaultAsync(j => j.Id == id);

            return job is not null ? Results.Ok(job) : Results.NotFound();
        });
    }
}