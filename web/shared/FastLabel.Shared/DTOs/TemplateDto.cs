using System.ComponentModel.DataAnnotations;

namespace FastLabel.Shared.DTOs;

public class TemplateDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "template.error.name_required")]
    [StringLength(150, ErrorMessage = "template.error.name_max_length")]
    public string Name { get; set; } = string.Empty;

    public string BasicStatusName { get; set; } = string.Empty;

    [Required(ErrorMessage = "template.error.json_data_required")]
    public string JsonData { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}