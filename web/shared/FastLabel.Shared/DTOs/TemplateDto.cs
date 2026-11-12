namespace FastLabel.Shared.DTOs;

public class TemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BasicStatusName { get; set; } = string.Empty; // Pour afficher le libellé du statut
    public string JsonData { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}