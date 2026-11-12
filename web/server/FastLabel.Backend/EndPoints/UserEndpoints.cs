using System.Security.Claims;
using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using FastLabel.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/users/me", async (AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var userIdString = claimsUser.FindFirstValue(ClaimTypes.NameIdentifier) 
                            ?? claimsUser.FindFirstValue("nameid");

            if (!int.TryParse(userIdString, out var userId))
                return Results.Unauthorized();

            var user = await db.Users
                .OrderBy(u => u.Id) 
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return Results.NotFound(new { message = "User not found." });

            return Results.Ok(new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                IsAdmin = user.IsAdmin,
                CreatedAt = user.CreatedAt
            });
        }).RequireAuthorization();

        var adminGroup = routes.MapGroup("/api/users").RequireAuthorization("AdminOnly");

        adminGroup.MapGet("/", async (AppDbContext db) =>
        {
            var users = await db.Users
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(users);
        });

        adminGroup.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound();

            var dto = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                IsAdmin = user.IsAdmin,
                CreatedAt = user.CreatedAt
            };

            return Results.Ok(dto);
        });

        adminGroup.MapPost("/", async (UserDto dto, AppDbContext db) =>
        {
            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                IsAdmin = dto.IsAdmin,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            dto.Id = user.Id;
            return Results.Created($"/api/users/{user.Id}", dto);
        });

        adminGroup.MapPut("/{id:int}", async (int id, UserDto updatedDto, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound();

            user.Name = updatedDto.Name;
            user.Email = updatedDto.Email;
            user.IsAdmin = updatedDto.IsAdmin;

            await db.SaveChangesAsync();
            return Results.Ok(user);
        });

        adminGroup.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound();

            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}