using System.ComponentModel.DataAnnotations;

namespace FastLabel.Backend.Models;

public class Text
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Tag { get; set; } = string.Empty;

    public string DefaultTranslation { get; set; } = string.Empty;

    public ICollection<Language> LanguagesDescriptions { get; set; } = new List<Language>();
    public ICollection<Translation> Translations { get; set; } = new List<Translation>();
}