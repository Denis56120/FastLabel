namespace FastLabel.Backend.Models;

public class PrintJob
{
    public int Id { get; set; }
    public string JobGuid { get; set; } = Guid.NewGuid().ToString();
    
    public int AgentId { get; set; }
    public int? PrinterId { get; set; } // <- Ajouté
    public string TargetIp { get; set; } = string.Empty;
    public int TargetPort { get; set; }

    public int? TemplateId { get; set; } // <- Ajouté
    public string InputDataJson { get; set; } = "{}"; // <- Ajouté
    public string GeneratedPayload { get; set; } = string.Empty; // <- Ajouté

    public string Status { get; set; } = "Pending";
    public string ResponseMessage { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}