namespace FastLabel.Shared.DTOs;

public class CreateManagedClientDto
{
    [Required(ErrorMessage = "user.error.name_required")]
    [StringLength(100, ErrorMessage = "user.error.name_max_length")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "user.error.email_required")]
    [EmailAddress(ErrorMessage = "user.error.email_invalid")]
    public string Email { get; set; } = string.Empty;

    public int LanguageId { get; set; }
}