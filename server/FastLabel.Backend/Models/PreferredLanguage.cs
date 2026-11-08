using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FastLabel.Backend.Models;

public class PreferredLanguage
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Locale { get; set; } = string.Empty;

    public int DescriptionTextId { get; set; }

    [ForeignKey(nameof(DescriptionTextId))]
    public Text Description { get; set; } = null!;

    public byte[]? Flag { get; set; }

    // Relation inverse
    public ICollection<Translation> Translations { get; set; } = [];
}