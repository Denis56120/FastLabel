using System.Security.Claims;
using FastLabel.Backend.Data;
using FastLabel.Backend.DTOs;
using FastLabel.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController(AppDbContext db) : ControllerBase
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    // 1. Récupérer le profil de l'utilisateur connecté
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out var userId))
            return Unauthorized();

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(new { message = "User not found." });

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            createdAt = user.CreatedAt
        });
    }

    // 2. Mettre à jour les préférences
    [HttpPut("me/preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesDto dto)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out var userId))
            return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "User not found." });

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "Preferences updated successfully."
        });
    }
}