using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

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

    public byte[]? Flag { get; set; }

    public ICollection<Translation> Translations { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];
}