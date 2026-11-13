namespace FastLabel.Shared.DTOs;

public class LabelDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "label.error.name_required")]
    [StringLength(150, ErrorMessage = "label.error.name_max_length")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "label.error.content_required")]
    public string Content { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "label.error.description_max_length")]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}