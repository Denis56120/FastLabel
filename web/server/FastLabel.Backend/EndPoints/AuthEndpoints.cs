using FastLabel.Backend.Data;
using FastLabel.Shared.DTOs;
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
            
            // Correction : Utilisation de BCrypt.Verify
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
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

        // AJOUT : Endpoint de réinitialisation de mot de passe
        group.MapPost("/forgot-password", async (ForgotPasswordDto dto, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return Results.BadRequest(new { message = "Email is required." });
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            // Par mesure de sécurité contre l'énumération d'utilisateurs, on renvoie toujours Ok
            if (user == null)
            {
                return Results.Ok(new { message = "If the email exists, a reset link has been sent." });
            }

            // Génération d'un token temporaire (nécessite les propriétés PasswordResetToken et ResetTokenExpires sur ton entité User si tu veux les stocker)
            user.PasswordResetToken = Guid.NewGuid().ToString();
            user.ResetTokenExpired = DateTime.UtcNow.AddHours(2);
            await db.SaveChangesAsync();

            // TODO : Insérer ici l'appel à ton service d'envoi d'e-mail (ex: _emailService.Send...)

            return Results.Ok(new { message = "If the email exists, a reset link has been sent." });
        });
    }
}