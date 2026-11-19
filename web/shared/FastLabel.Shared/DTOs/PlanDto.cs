namespace FastLabel.Shared.DTOs;

public class PlanDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public decimal MonthlyPrice { get; set; }

    public decimal YearlyPrice { get; set; }

    public int MonthlyPrintsIncluded { get; set; }

    public int? TopUpPrintsIncluded { get; set; }

    public decimal? TopUpPrice { get; set; }

    public string? StripeMonthlyPriceId { get; set; }

    public string? StripeYearlyPriceId { get; set; }

    public string? StripeTopUpPriceId { get; set; }

    public bool IsFree => string.Equals(Code, "free", StringComparison.OrdinalIgnoreCase)
                         || (MonthlyPrice <= 0 && YearlyPrice <= 0);

    public bool HasTopUp => TopUpPrintsIncluded.GetValueOrDefault() > 0
                          && TopUpPrice.GetValueOrDefault() > 0;
}