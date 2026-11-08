using FastLabel.Backend.Data;
using FastLabel.Backend.DTOs;
using FastLabel.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, AppDbContext db, TokenService tokenService) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            
            if (user == null || user.PasswordHash != request.Password)
            {
                return Results.BadRequest(new { message = "Identifiants invalides." });
            }

            var token = tokenService.GenerateToken(user);

            return Results.Ok(new
            {
                token,
                user = new { user.Id, user.Email, user.Name } 
            });
        });
    }
}