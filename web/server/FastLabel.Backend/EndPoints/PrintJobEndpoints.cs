namespace FastLabel.Backend.Endpoints;

public static class PrintJobEndpoints
{
    public static void MapPrintJobEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/printjobs").RequireAuthorization();

        static (int userId, bool isAdmin) GetUserInfo(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? user.FindFirst("nameid")?.Value 
                           ?? user.FindFirst("unique_name")?.Value;

            int.TryParse(userIdClaim, out var userId);
            bool isAdmin = user.IsInRole("Admin");

            return (userId, isAdmin);
        }

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
            // Valeurs par défaut de sécurité
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 500) pageSize = 500; // Limite maximale de sécurité par page

            var (userId, isAdmin) = GetUserInfo(claimsUser);
            if (userId == 0 && !isAdmin) return Results.Unauthorized();

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

            // 2. Calcul du nombre total d'éléments après filtrage et avant la pagination
            var totalCount = await query.CountAsync();

            // 3. Application du tri dynamique (basé sur les en-têtes cliqués)
            bool ascending = sortAscending ?? true;
            query = sortBy?.ToLower() switch
            {
                "username" => ascending ? query.OrderBy(j => j.Printer.User.Name) : query.OrderByDescending(j => j.Printer.User.Name),
                "printername" => ascending ? query.OrderBy(j => j.Printer.Name) : query.OrderByDescending(j => j.Printer.Name),
                "templatename" => ascending ? query.OrderBy(j => j.LabelTemplate.Name) : query.OrderByDescending(j => j.LabelTemplate.Name),
                "quantity" => ascending ? query.OrderBy(j => j.Quantity) : query.OrderByDescending(j => j.Quantity),
                "createdat" => ascending ? query.OrderBy(j => j.CreatedAt) : query.OrderByDescending(j => j.CreatedAt),
                _ => query.OrderByDescending(j => j.CreatedAt) // Tri par défaut (du plus récent au plus ancien)
            };

            // 4. Application de la pagination (Skip & Take)
            var jobs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                .ToListAsync();

            // 5. Encapsulation dans le PagedResultDto
            var result = new PagedResultDto<PrintJobsDto>
            {
                Items = jobs,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Results.Ok(result);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);
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