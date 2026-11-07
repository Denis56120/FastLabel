using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints; // <- Le namespace doit correspondre au using dans Program.cs

public static class PrinterEndpoints // <- La classe doit être static
{
    public static void MapPrinterEndpoints(this IEndpointRouteBuilder routes) // <- Doit être public static avec 'this'
    {
        var group = routes.MapGroup("/api/printers");

        group.MapGet("/", async (AppDbContext db) =>
        {
            var printers = await db.Printers.ToListAsync();
            return Results.Ok(printers);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var printer = await db.Printers.FindAsync(id);
            return printer is not null ? Results.Ok(printer) : Results.NotFound();
        });

        group.MapPost("/", async (Printer printer, AppDbContext db) =>
        {
            db.Printers.Add(printer);
            await db.SaveChangesAsync();
            return Results.Created($"/api/printers/{printer.Id}", printer);
        });

        group.MapPut("/{id:int}", async (int id, Printer updatedPrinter, AppDbContext db) =>
        {
            var printer = await db.Printers.FindAsync(id);
            if (printer is null) return Results.NotFound();

            printer.Name = updatedPrinter.Name;
            printer.Location = updatedPrinter.Location;
            printer.Ip = updatedPrinter.Ip;
            printer.Port = updatedPrinter.Port;
            printer.Protocol = updatedPrinter.Protocol;
            printer.ResolutionDpi = updatedPrinter.ResolutionDpi;
            printer.IsActive = updatedPrinter.IsActive;

            await db.SaveChangesAsync();
            return Results.Ok(printer);
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var printer = await db.Printers.FindAsync(id);
            if (printer is null) return Results.NotFound();

            db.Printers.Remove(printer);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}