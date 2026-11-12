namespace FastLabel.Shared.DTOs;

public class AgentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // Si tu souhaites garder un nom d'agent (ou tu peux l'adapter selon tes champs)
    public string AgentTypeName { get; set; } = string.Empty; // Pour afficher le nom du type d'agent plutôt que l'ID
    public string ApiKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
}