using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FastLabel.Backend.Models;
using Microsoft.IdentityModel.Tokens;

namespace FastLabel.Backend.Services;

public class TokenService(IConfiguration config)
{
    private readonly IConfiguration _config = config;

    public string GenerateToken(User user)
    {
        var secretKey = _config["Jwt:SecretKey"] ?? "SUPER_SECRET_KEY_MIN_32_CHARS_FASTLABEL_2026!";
        var key = Encoding.UTF8.GetBytes(secretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new("lang", user.PreferredLanguage)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}