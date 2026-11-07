using System.Security.Claims;
using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Controllers;

[Authorize] // Nécessite d'être authentifié avec le token JWT
[ApiController]
[Route("api/[controller]")]
public class UsersController(AppDbContext db) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly PasswordHasher<User> _passwordHasher = new();

    // 1. Récupérer le profil de l'utilisateur connecté
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Subscription)
            .Include(u => u.Agents)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(new { message = "User not found." });

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            preferredLanguage = user.PreferredLanguage,
            createdAt = user.CreatedAt,
            subscription = user.Subscription,
            agentsCount = user.Agents.Count
        });
    }

    // 2. Mettre à jour les préférences (ex: langue)
    [HttpPut("me/preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesDto dto)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "User not found." });

        if (!string.IsNullOrEmpty(dto.PreferredLanguage))
        {
            var lang = dto.PreferredLanguage.ToLower();
            user.PreferredLanguage = (lang == "fr" || lang == "en") ? lang : "en";
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Preferences updated successfully.",
            preferredLanguage = user.PreferredLanguage
        });
    }
}

// DTO pour la mise à jour des préférences
public class UpdatePreferencesDto
{
    public string PreferredLanguage { get; set; } = "en";
}