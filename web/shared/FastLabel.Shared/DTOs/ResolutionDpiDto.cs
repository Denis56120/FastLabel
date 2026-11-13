using System.ComponentModel.DataAnnotations;

namespace FastLabel.Shared.DTOs;

public class ResolutionDpiDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "resolutions.error.value_required")]
    [Range(1, 10000, ErrorMessage = "resolutions.error.value_range")]
    public int Value { get; set; }
}