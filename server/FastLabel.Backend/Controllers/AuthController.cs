using FastLabel.Backend.Data;
using FastLabel.Backend.DTOs;
using FastLabel.Backend.Models;
using FastLabel.Backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, TokenService tokenService) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly TokenService _tokenService = tokenService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var existingUser = await _db.Users.AnyAsync(u => u.Email == normalizedEmail);
        if (existingUser)
        {
            return BadRequest(new { message = "Email already registered / Email déjà utilisé." });
        }

        var user = new User
        {
            Email = normalizedEmail,
            PreferredLanguage = dto.PreferredLanguage.Equals("fr", StringComparison.CurrentCultureIgnoreCase) ? "fr" : "en"
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokenService.GenerateToken(user);

        return Ok(new AuthResponseDto(
            token,
            user.Id,
            user.Email,
            user.PreferredLanguage,
            DateTime.UtcNow.AddDays(7)
        ));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or password / Identifiants invalides." });
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid email or password / Identifiants invalides." });
        }

        var token = _tokenService.GenerateToken(user);

        return Ok(new AuthResponseDto(
            token,
            user.Id,
            user.Email,
            user.PreferredLanguage,
            DateTime.UtcNow.AddDays(7)
        ));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant());
        
        // Par sécurité, on renvoie un OK même si l'email n'existe pas pour éviter l'énumération de comptes
        if (user == null)
        {
            return Ok(new { message = "If the email exists, a reset link has been sent." });
        }

        // Génération d'un token aléatoire sécurisé (valable 1 heure)
        user.PasswordResetToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray());
        user.ResetTokenExpires = DateTime.UtcNow.AddHours(1);

        await _db.SaveChangesAsync();

        // TODO (Production) : Envoyer le token par e-mail via un service SMTP/SendGrid
        // Pour le dev, on peut le logger ou le retourner temporairement si besoin.
        Console.WriteLine($"[RESET PASSWORD TOKEN] Pour {user.Email} : {user.PasswordResetToken}");

        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant());

        if (user == null || user.PasswordResetToken != dto.Token || user.ResetTokenExpires < DateTime.UtcNow)
        {
            return BadRequest(new { message = "Invalid or expired token / Jeton invalide ou expiré." });
        }

        // Hashage du nouveau mot de passe
        user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
        
        // Invalidation du token utilisé
        user.PasswordResetToken = null;
        user.ResetTokenExpires = null;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Password successfully reset / Mot de passe réinitialisé avec succès." });
    }
}