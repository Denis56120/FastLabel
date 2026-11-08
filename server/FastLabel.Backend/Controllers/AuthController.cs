using FastLabel.Backend.Data;
using FastLabel.Backend.DTOs;
using FastLabel.Backend.Models;
using FastLabel.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, TokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || user.PasswordHash != request.Password)
        {
            return BadRequest(new { message = "Identifiants invalides." });
        }

        var token = tokenService.GenerateToken(user);

        return Ok(new
        {
            token,
            user = new { user.Id, user.Email, user.Name }
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest(new { message = "Cet email est déjà utilisé." });
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = request.Password, // Idéalement hashé avec PasswordHasher
            Name = request.Name,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = tokenService.GenerateToken(user);

        return Ok(new
        {
            token,
            user = new { user.Id, user.Email, user.Name }
        });
    }
}