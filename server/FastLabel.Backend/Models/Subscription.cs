using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FastLabel.Backend.Models;

public class Subscription
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required, MaxLength(100)]
    public string StripeCustomerId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? StripeSubscriptionId { get; set; }

    [Required, MaxLength(50)]
    public string Status { get; set; } = "trialing"; // active, trialing, canceled, past_due

    [MaxLength(50)]
    public string PlanId { get; set; } = "starter_monthly";

    public DateTime CurrentPeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}