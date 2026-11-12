namespace FastLabel.Shared.DTOs;

public class PrintHistoryDto
{
    public int Id { get; set; }
    
    // Noms des entités liées pour l'affichage dans le tableau
    public string PrinterName { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    
    public int Quantity { get; set; }
    public string InputDataJson { get; set; } = string.Empty;
    public string? GeneratedPayload { get; set; }
    public string? ResponseMessage { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}