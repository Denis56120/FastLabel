using System.ComponentModel.DataAnnotations;

namespace FastLabel.Shared.DTOs;

public class PrintJobsDto
{
    public int Id { get; set; }    

    [Required(ErrorMessage = "printjobs.error.printer_name_required")]
    public string PrinterName { get; set; } = string.Empty;

    [Required(ErrorMessage = "printjobs.error.template_name_required")]
    public string TemplateName { get; set; } = string.Empty;
    
    [Required]
    [Range(1, 10000, ErrorMessage = "printjobs.error.quantity_range")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "printjobs.error.input_data_required")]
    public string InputDataJson { get; set; } = string.Empty;

    public string? GeneratedPayload { get; set; }
    public string? ResponseMessage { get; set; }

    public string UserName { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}