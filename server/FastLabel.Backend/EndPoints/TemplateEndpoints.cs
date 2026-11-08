using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/templates");

        group.MapGet("/", async (AppDbContext db) =>
        {
            var templates = await db.LabelTemplates.ToListAsync();
            return Results.Ok(templates);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var template = await db.LabelTemplates.FindAsync(id);
            return template is not null ? Results.Ok(template) : Results.NotFound();
        });

        group.MapPost("/", async (LabelTemplate template, AppDbContext db) =>
        {
            db.LabelTemplates.Add(template);
            await db.SaveChangesAsync();
            return Results.Created($"/api/templates/{template.Id}", template);
        });

        group.MapPut("/{id:int}", async (int id, LabelTemplate updated, AppDbContext db) =>
        {
            var template = await db.LabelTemplates.FindAsync(id);
            if (template is null) return Results.NotFound();

            template.Name = updated.Name;
            template.JsonData = updated.JsonData;

            await db.SaveChangesAsync();
            return Results.Ok(template);
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var template = await db.LabelTemplates.FindAsync(id);
            if (template is null) return Results.NotFound();

            db.LabelTemplates.Remove(template);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}