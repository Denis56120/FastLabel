namespace FastLabel.Shared.DTOs;

public class SubscriptionDto
{
    public int Id { get; set; }

    public string PlanCode { get; set; } = "free";

    [Required(ErrorMessage = "subscription.error.plan_required")]
    public string PlanName { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "subscription.error.period_invalid")]
    public int SubscriptionPeriod { get; set; }

    public decimal MonthlyPrice { get; set; }

    public decimal YearlyPrice { get; set; }

    public int MonthlyPrintsIncluded { get; set; }

    public int PrintsUsedThisMonth { get; set; }

    public int ExtraPrintsBalance { get; set; }

    public int TotalAvailablePrints =>
        Math.Max(0, MonthlyPrintsIncluded - PrintsUsedThisMonth) + ExtraPrintsBalance;

    public int? TopUpPrintsIncluded { get; set; }

    public decimal? TopUpPrice { get; set; }

    public DateTime? CurrentPeriodStart { get; set; }

    public DateTime? CurrentPeriodEnd { get; set; }

    public bool HasStripeSubscription { get; set; }

    public DateTime CreatedAt { get; set; }
}