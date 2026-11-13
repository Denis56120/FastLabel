namespace FastLabel.Shared.DTOs;

public class AgentDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "agent.error.name_required")]
    [StringLength(100, ErrorMessage = "agent.error.name_max_length")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "agent.error.type_required")]
    public string AgentTypeName { get; set; } = string.Empty;

    [Required(ErrorMessage = "agent.error.apikey_required")]
    public string ApiKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
}