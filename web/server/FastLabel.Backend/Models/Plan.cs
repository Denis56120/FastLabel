namespace FastLabel.Backend.Models;

public class Plan
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    public int NameTextId { get; set; }

    [ForeignKey(nameof(NameTextId))]
    public Text Name { get; set; } = null!;

    public int MonthlyPrintsIncluded { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal MonthlyPrice { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal YearlyPrice { get; set; }

    public int? TopUpPrintsIncluded { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? TopUpPrice { get; set; }

    [MaxLength(100)]
    public string? StripeProductId { get; set; }

    [MaxLength(100)]
    public string? StripeMonthlyPriceId { get; set; }

    [MaxLength(100)]
    public string? StripeYearlyPriceId { get; set; }

    [MaxLength(100)]
    public string? StripeTopUpPriceId { get; set; }

    public int SortOrder { get; set; }

    public int BasicStatusId { get; set; }

    [ForeignKey(nameof(BasicStatusId))]
    public BasicStatus BasicStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}