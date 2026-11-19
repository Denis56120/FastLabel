namespace FastLabel.Backend.Models;

public class Subscription
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public int PlanId { get; set; }

    [ForeignKey(nameof(PlanId))]
    public Plan Plan { get; set; } = null!;

    [Required]
    public int SubscriptionPeriod { get; set; }

    public int? StripeId { get; set; }

    [ForeignKey(nameof(StripeId))]
    public StripeRecord? Stripe { get; set; }

    public DateTime? CurrentPeriodStart { get; set; }

    public DateTime? CurrentPeriodEnd { get; set; }

    public int ExtraPrintsBalance { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}