namespace FastLabel.Shared.DTOs;

public class UserDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "user.error.name_required")]
    [StringLength(100, ErrorMessage = "user.error.name_max_length")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "user.error.email_required")]
    [EmailAddress(ErrorMessage = "user.error.email_invalid")]
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }

    public bool IsAdmin { get; set; }
    public string BasicStatusName { get; set; } = string.Empty; 
    public DateTime CreatedAt { get; set; }
}