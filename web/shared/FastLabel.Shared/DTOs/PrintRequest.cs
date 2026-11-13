namespace FastLabel.Shared.DTOs;

public class PrintRequest
{
    [Required(ErrorMessage = "print.error.apikey_required")]
    public string ApiKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "print.error.printer_id_required")]
    [Range(1, int.MaxValue, ErrorMessage = "print.error.printer_id_invalid")]
    public int PrinterId { get; set; }

    public int? TemplateId { get; set; }

    [Required(ErrorMessage = "print.error.quantity_required")]
    [Range(1, 10000, ErrorMessage = "print.error.quantity_range")]
    public int Quantity { get; set; }

    public string? RawData { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
}