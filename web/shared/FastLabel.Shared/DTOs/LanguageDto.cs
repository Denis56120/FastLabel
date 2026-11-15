using System.ComponentModel.DataAnnotations;

namespace FastLabel.Shared.DTOs;

public class LanguageDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "language.error.code_required")]
    [StringLength(10, ErrorMessage = "language.error.code_max_length")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "language.error.name_required")]
    public string Name { get; set; } = string.Empty;

    public string? FlagBase64 { get; set; }
}