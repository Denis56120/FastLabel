namespace FastLabel.Backend.Endpoints;

public static class PrintJobEndpoints
{
    public static void MapPrintJobEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/jobs").RequireAuthorization();

        static (int userId, bool isAdmin) GetUserInfo(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? user.FindFirst("nameid")?.Value 
                           ?? user.FindFirst("unique_name")?.Value;

            int.TryParse(userIdClaim, out var userId);
            bool isAdmin = user.IsInRole("Admin");

            return (userId, isAdmin);
        }

        group.MapGet("/", async (AppDbContext db, int? limit, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);

            var query = db.PrintJobs
                .Include(j => j.Printer)
                .Include(j => j.LabelTemplate)
                .OrderByDescending(j => j.CreatedAt)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(j => j.Printer.UserId == userId);
            }

            var takeCount = limit.HasValue ? limit.Value : 100;

            var jobs = await query
                .Take(takeCount)
                .Select(j => new PrintHistoryDto
                {
                    Id = j.Id,
                    PrinterName = j.Printer != null ? j.Printer.Name : string.Empty,
                    TemplateName = j.LabelTemplate != null ? j.LabelTemplate.Name : string.Empty,
                    Quantity = j.Quantity,
                    InputDataJson = j.InputDataJson ?? string.Empty,
                    GeneratedPayload = j.GeneratedPayload,
                    ResponseMessage = j.ResponseMessage,
                    CreatedAt = j.CreatedAt,
                    ProcessedAt = j.ProcessedAt
                })
                .ToListAsync();

            return Results.Ok(jobs);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);

            var query = db.PrintJobs
                .Include(j => j.Printer)
                .Include(j => j.LabelTemplate)
                .Where(j => j.Id == id)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(j => j.Printer.UserId == userId);
            }

            var jobDto = await query
                .OrderBy(j => j.Id)
                .Select(j => new PrintHistoryDto
                {
                    Id = j.Id,
                    PrinterName = j.Printer != null ? j.Printer.Name : string.Empty,
                    TemplateName = j.LabelTemplate != null ? j.LabelTemplate.Name : string.Empty,
                    Quantity = j.Quantity,
                    InputDataJson = j.InputDataJson ?? string.Empty,
                    GeneratedPayload = j.GeneratedPayload,
                    ResponseMessage = j.ResponseMessage,
                    CreatedAt = j.CreatedAt,
                    ProcessedAt = j.ProcessedAt
                })
                .FirstOrDefaultAsync();

            return jobDto is not null ? Results.Ok(jobDto) : Results.NotFound();
        });
    }
}