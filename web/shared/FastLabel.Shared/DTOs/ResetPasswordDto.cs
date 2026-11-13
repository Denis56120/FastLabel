namespace FastLabel.Shared.DTOs;

public class ResetPasswordDto
{
    [Required(ErrorMessage = "auth.error.email_required")]
    [EmailAddress(ErrorMessage = "auth.error.email_invalid")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "auth.error.token_required")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "auth.error.password_required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    public string NewPassword { get; set; } = string.Empty;
}