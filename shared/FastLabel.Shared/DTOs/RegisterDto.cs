using System.ComponentModel.DataAnnotations;

namespace FastLabel.Backend.DTOs;

public class RegisterDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    public string Password { get; set; } = string.Empty;

    public string PreferredLanguage { get; set; } = "en"; // "fr" ou "en"
}