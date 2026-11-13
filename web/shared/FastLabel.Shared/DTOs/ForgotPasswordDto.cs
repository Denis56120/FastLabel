namespace FastLabel.Shared.DTOs;

public class ForgotPasswordDto
{
    [Required(ErrorMessage = "auth.error.email_required")]
    [EmailAddress(ErrorMessage = "auth.error.email_invalid")]
    public string Email { get; set; } = string.Empty;
}