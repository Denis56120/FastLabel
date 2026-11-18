namespace FastLabel.Backend.Endpoints;

public static class PrintJobEndpoints
{
    public static void MapPrintJobEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/printjobs").RequireAuthorization();

        group.MapGet("/", async (
            int page, 
            int pageSize, 
            string? sortBy, 
            bool? sortAscending, 
            string? filterByUser,
            string? filterByPrinter,
            string? filterByTemplate,
            AppDbContext db, 
            ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0 && !isAdmin) return Results.Unauthorized();

            // Valeurs par défaut de sécurité
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 500) pageSize = 500; // Limite maximale de sécurité par page

            var query = db.PrintJobs
                .Include(j => j.Printer)
                    .ThenInclude(p => p.User) 
                .Include(j => j.LabelTemplate)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(j => j.Printer.UserId == userId);
            }

            // 1. Application des filtres textuels reçus du front
            if (!string.IsNullOrWhiteSpace(filterByUser))
            {
                query = query.Where(j => j.Printer != null && j.Printer.User != null && j.Printer.User.Name.Contains(filterByUser));
            }

            if (!string.IsNullOrWhiteSpace(filterByPrinter))
            {
                query = query.Where(j => j.Printer != null && j.Printer.Name.Contains(filterByPrinter));
            }

            if (!string.IsNullOrWhiteSpace(filterByTemplate))
            {
                query = query.Where(j => j.LabelTemplate != null && j.LabelTemplate.Name.Contains(filterByTemplate));
            }

            var naturalComparer = new NaturalStringHelpers();

            // 2. Utilisation de l'extension ToPagedResultAsync pour la pagination et le tri naturel
            var result = await query.ToPagedResultAsync(
                page,
                pageSize,
                sortBy,
                sortAscending,
                j => new PrintJobsDto
                {
                    Id = j.Id,
                    PrinterName = j.Printer != null ? j.Printer.Name : string.Empty,
                    TemplateName = j.LabelTemplate != null ? j.LabelTemplate.Name : string.Empty,
                    Quantity = j.Quantity,
                    InputDataJson = j.InputDataJson ?? string.Empty,
                    GeneratedPayload = j.GeneratedPayload,
                    ResponseMessage = j.ResponseMessage,
                    UserName = j.Printer != null && j.Printer.User != null ? j.Printer.User.Name : string.Empty, 
                    CreatedAt = j.CreatedAt,
                    ProcessedAt = j.ProcessedAt
                },
                (items, sortField, ascending) => sortField?.ToLower() switch
                {
                    "username" => ascending ? items.OrderBy(x => x.UserName, naturalComparer) : items.OrderByDescending(x => x.UserName, naturalComparer),
                    "printername" => ascending ? items.OrderBy(x => x.PrinterName, naturalComparer) : items.OrderByDescending(x => x.PrinterName, naturalComparer),
                    "templatename" => ascending ? items.OrderBy(x => x.TemplateName, naturalComparer) : items.OrderByDescending(x => x.TemplateName, naturalComparer),
                    "quantity" => ascending ? items.OrderBy(x => x.Quantity) : items.OrderByDescending(x => x.Quantity),
                    "createdat" => ascending ? items.OrderBy(x => x.CreatedAt) : items.OrderByDescending(x => x.CreatedAt),
                    _ => items.OrderByDescending(x => x.CreatedAt) // Tri par défaut (du plus récent au plus ancien)
                }
            );

            return Results.Ok(result);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0 && !isAdmin) return Results.Unauthorized();

            var query = db.PrintJobs
                .Include(j => j.Printer)
                    .ThenInclude(p => p.User) 
                .Include(j => j.LabelTemplate)
                .Where(j => j.Id == id)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(j => j.Printer.UserId == userId);
            }

            var jobDto = await query
                .Select(j => new PrintJobsDto
                {
                    Id = j.Id,
                    PrinterName = j.Printer != null ? j.Printer.Name : string.Empty,
                    TemplateName = j.LabelTemplate != null ? j.LabelTemplate.Name : string.Empty,
                    Quantity = j.Quantity,
                    InputDataJson = j.InputDataJson ?? string.Empty,
                    GeneratedPayload = j.GeneratedPayload,
                    ResponseMessage = j.ResponseMessage,
                    UserName = j.Printer != null && j.Printer.User != null ? j.Printer.User.Name : string.Empty,
                    CreatedAt = j.CreatedAt,
                    ProcessedAt = j.ProcessedAt
                })
                .FirstOrDefaultAsync();

            return jobDto is not null 
                ? Results.Ok(jobDto) 
                : Results.NotFound(new ErrorResponseDto { Message = "job.error.not_found" });
        });
    }
}