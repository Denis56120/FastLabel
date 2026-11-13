using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    public int SubscriptionTypeId { get; set; }

    [ForeignKey(nameof(SubscriptionTypeId))]
    public SubscriptionType SubscriptionType { get; set; } = null!;

    public int StripeId { get; set; }

    [ForeignKey(nameof(StripeId))]
    public Stripe Stripe { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}