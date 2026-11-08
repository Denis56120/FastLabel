using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FastLabel.Backend.Models;

public class PrintJob
{
    [Key]
    public int Id { get; set; }

    public int PrinterId { get; set; }

    [ForeignKey(nameof(PrinterId))]
    public Printer Printer { get; set; } = null!;

    public int AgentId { get; set; }

    // [ForeignKey(nameof(AgentId))]
    // public Agent Agent { get; set; } = null!; // Sera activé une fois la table Agent définie

    public int LabelTemplateId { get; set; }

    [ForeignKey(nameof(LabelTemplateId))]
    public LabelTemplate LabelTemplate { get; set; } = null!;

    [Required]
    public int Quantity { get; set; }

    [Required]
    public string InputDataJson { get; set; } = string.Empty;

    public string? GeneratedPayload { get; set; }

    public string? ResponseMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }
}