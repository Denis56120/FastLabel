namespace FastLabel.Shared.DTOs;

public class AgentDto
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "agents.error.type_required")]
    public int AgentTypeId { get; set; }

    public string? AgentTypeName { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastSeenAt { get; set; }
}