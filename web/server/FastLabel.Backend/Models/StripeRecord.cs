namespace FastLabel.Backend.Models;

public class StripeRecord
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string StripeCustomerId { get; set; } = string.Empty;

    [Required]
    public string StripeSubscriptionId { get; set; } = string.Empty;

    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}