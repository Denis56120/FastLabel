using System.ComponentModel.DataAnnotations;

namespace FastLabel.Shared.DTOs;

public class RegisterDto
{
    [Required(ErrorMessage = "auth.error.name_required")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "auth.error.email_required")]
    [EmailAddress(ErrorMessage = "auth.error.email_invalid")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "auth.error.password_required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    public string Password { get; set; } = string.Empty;
}