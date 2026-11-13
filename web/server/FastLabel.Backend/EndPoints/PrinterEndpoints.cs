using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using FastLabel.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FastLabel.Backend.Endpoints;

public static class PrinterEndpoints
{
    public static void MapPrinterEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/printers").RequireAuthorization();

        static (int userId, bool isAdmin) GetUserInfo(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? user.FindFirst("nameid")?.Value
                           ?? user.FindFirst("unique_name")?.Value;

            int.TryParse(userIdClaim, out var userId);
            bool isAdmin = user.IsInRole("Admin");

            return (userId, isAdmin);
        }

        group.MapGet("/", async (AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var query = db.Printers
                .Include(p => p.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(t => t.Translations)
                            .ThenInclude(tr => tr.Language)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(p => p.UserId == userId);
            }

            var printers = await query
                .Select(p => new PrinterDto
                {
                    Id = p.Id,
                    Name = p.Name ?? string.Empty,
                    Location = p.Location ?? string.Empty,
                    IP = p.IP ?? string.Empty,
                    Port = p.Port,
                    StatusName = p.BasicStatus != null && p.BasicStatus.Name != null 
                        ? (p.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault() 
                           ?? p.BasicStatus.Name.DefaultTranslation) 
                        : string.Empty,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(printers);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var query = db.Printers
                .Include(p => p.BasicStatus)
                    .ThenInclude(s => s.Name)
                        .ThenInclude(t => t.Translations)
                            .ThenInclude(tr => tr.Language)
                .Where(p => p.Id == id)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(p => p.UserId == userId);
            }

            var printer = await query
                .OrderBy(p => p.Id)
                .Select(p => new PrinterDto
                {
                    Id = p.Id,
                    Name = p.Name ?? string.Empty,
                    Location = p.Location ?? string.Empty,
                    IP = p.IP ?? string.Empty,
                    Port = p.Port,
                    StatusName = p.BasicStatus != null && p.BasicStatus.Name != null 
                        ? (p.BasicStatus.Name.Translations
                            .Where(tr => tr.Language.Code == lang)
                            .Select(tr => tr.TranslatedValue)
                            .FirstOrDefault() 
                           ?? p.BasicStatus.Name.DefaultTranslation) 
                        : string.Empty,
                    CreatedAt = p.CreatedAt
                })
                .FirstOrDefaultAsync();

            return printer is not null ? Results.Ok(printer) : Results.NotFound();
        });

        group.MapPost("/", async (PrinterDto dto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, _) = GetUserInfo(claimsUser);
            if (userId == 0) return Results.Unauthorized();

            var printer = new Printer
            {
                UserId = userId,
                Name = dto.Name,
                Location = dto.Location,
                IP = dto.IP,
                Port = dto.Port,
                ProtocolId = 1,
                ResolutionDpiId = 1,
                BasicStatusId = 1,
                CreatedAt = DateTime.UtcNow
            };

            db.Printers.Add(printer);
            await db.SaveChangesAsync();

            dto.Id = printer.Id;
            return Results.Created($"/api/printers/{printer.Id}", dto);
        });

        group.MapPut("/{id:int}", async (int id, PrinterDto updatedDto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);

            var printer = await db.Printers.FindAsync(id);
            if (printer is null) return Results.NotFound();

            if (!isAdmin && printer.UserId != userId)
            {
                return Results.Forbid();
            }

            printer.Name = updatedDto.Name;
            printer.Location = updatedDto.Location;
            printer.IP = updatedDto.IP;
            printer.Port = updatedDto.Port;

            await db.SaveChangesAsync();
            return Results.Ok(printer);
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);

            var printer = await db.Printers.FindAsync(id);
            if (printer is null) return Results.NotFound();

            if (!isAdmin && printer.UserId != userId)
            {
                return Results.Forbid();
            }

            db.Printers.Remove(printer);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}