using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FastLabel.Backend.Models;

public class Translation
{
    [Key]
    public int Id { get; set; }

    public int PreferredLanguageId { get; set; }

    [ForeignKey(nameof(PreferredLanguageId))]
    public PreferredLanguage PreferredLanguage { get; set; } = null!;

    public int TextId { get; set; }

    [ForeignKey(nameof(TextId))]
    public Text Text { get; set; } = null!;

    [Required]
    public string TranslatedValue { get; set; } = string.Empty;
}