namespace FastLabel.Shared.DTOs;

public class LoginRequest
{
    [Required(ErrorMessage = "auth.error.email_required")]
    [EmailAddress(ErrorMessage = "auth.error.email_invalid")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "auth.error.password_required")]
    public string Password { get; set; } = string.Empty;
}