namespace FastLabel.Backend.Models;

public class Stripe
{
    [Key]
    public int Id { get; set; }

    public int StripeCustomerId { get; set; }

    public int StripeSubscriptionId { get; set; }
}