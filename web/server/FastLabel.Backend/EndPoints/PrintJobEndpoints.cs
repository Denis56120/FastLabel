using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using FastLabel.Shared.DTOs;
using FastLabel.Backend.Services;

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

        group.MapGet("/", async (int page, int pageSize, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            // Valeurs par défaut de sécurité
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 500) pageSize = 500; // Limite maximale de sécurité par page

            var (userId, isAdmin) = GetUserInfo(claimsUser);
            if (userId == 0 && !isAdmin) return Results.Unauthorized();

            var query = db.PrintJobs
                .Include(j => j.Printer)
                    .ThenInclude(p => p.User) // <--- Inclusion de l'utilisateur via la relation Printer -> User
                .Include(j => j.LabelTemplate)
                .OrderByDescending(j => j.CreatedAt)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(j => j.Printer.UserId == userId);
            }

            // 1. Calcul du nombre total d'éléments avant la pagination
            var totalCount = await query.CountAsync();

            // 2. Application de la pagination (Skip & Take)
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

            // 3. Encapsulation dans le PagedResultDto
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
                    UserName = j.Printer != null && j.Printer.User != null ? j.Printer.User.Name : string.Empty, // <--- Assignation du UserName
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