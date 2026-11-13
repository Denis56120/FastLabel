namespace FastLabel.Shared.DTOs;

public class PrintHistoryDto
{
    public int Id { get; set; }    

    [Required(ErrorMessage = "printhistory.error.printer_name_required")]
    public string PrinterName { get; set; } = string.Empty;

    [Required(ErrorMessage = "printhistory.error.template_name_required")]
    public string TemplateName { get; set; } = string.Empty;
    
    [Required]
    [Range(1, 10000, ErrorMessage = "printhistory.error.quantity_range")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "printhistory.error.input_data_required")]
    public string InputDataJson { get; set; } = string.Empty;

    public string? GeneratedPayload { get; set; }
    public string? ResponseMessage { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}