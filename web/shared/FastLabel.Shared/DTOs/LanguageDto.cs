using System.ComponentModel.DataAnnotations;

namespace FastLabel.Shared.DTOs;

public class LanguageDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "language.error.code_required")]
    [StringLength(10, ErrorMessage = "language.error.code_max_length")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "language.error.description_required")]
    public string Description { get; set; } = string.Empty;

    public string? Flag { get; set; }
}