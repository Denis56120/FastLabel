using System.ComponentModel.DataAnnotations;

namespace FastLabel.Backend.Models;

public class Text
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Tag { get; set; } = string.Empty;

    public string DefaultTranslation { get; set; } = string.Empty;

    // Relations inverses
    public ICollection<PreferredLanguage> PreferredLanguagesDescriptions { get; set; } = new List<PreferredLanguage>();
    public ICollection<Translation> Translations { get; set; } = new List<Translation>();
}