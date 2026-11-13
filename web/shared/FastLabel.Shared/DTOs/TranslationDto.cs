namespace FastLabel.Shared.DTOs;

public class TranslationDto
{
    [Required(ErrorMessage = "translation.error.key_required")]
    public string Key { get; set; } = string.Empty;

    [Required(ErrorMessage = "translation.error.value_required")]
    public string Value { get; set; } = string.Empty;
}