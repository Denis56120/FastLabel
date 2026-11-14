namespace FastLabel.Shared.DTOs;

using System.ComponentModel.DataAnnotations;

public class StripeDto
{
    [Required(ErrorMessage = "stripe.error.plan_required")]
    [StringLength(50, ErrorMessage = "stripe.error.plan_max_length")]
    public string Plan { get; set; } = string.Empty; 

    [Required(ErrorMessage = "stripe.error.interval_required")]
    [StringLength(20, ErrorMessage = "stripe.error.interval_max_length")]
    public string BillingInterval { get; set; } = string.Empty; 
}