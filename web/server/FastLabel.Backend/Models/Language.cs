namespace FastLabel.Backend.Models;

[Index(nameof(Code), IsUnique = true)]
public class Language
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(10, ErrorMessage = "language.error.code_max_length")]
    public string Code { get; set; } = string.Empty;

    [Required]
    public int DescriptionTextId { get; set; }

    [ForeignKey(nameof(DescriptionTextId))]
    public Text Description { get; set; } = null!;

    public string Flag { get; set; } = string.Empty;

    public ICollection<Translation> Translations { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];
}