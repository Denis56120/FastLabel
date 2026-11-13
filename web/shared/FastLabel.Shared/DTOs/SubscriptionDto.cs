namespace FastLabel.Shared.DTOs;

public class SubscriptionDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "subscription.error.plan_required")]
    public string PlanName { get; set; } = string.Empty;

    [Required(ErrorMessage = "subscription.error.type_required")]
    public string SubscriptionTypeName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "subscription.error.period_invalid")]
    public int SubscriptionPeriod { get; set; }

    public DateTime CreatedAt { get; set; }
}