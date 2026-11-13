namespace FastLabel.Backend.Models;

[Index(nameof(ApiKey), IsUnique = true)]
public class Agent
{
    [Key]
    public int Id { get; set; }

    public int AgentTypeId { get; set; }

    [ForeignKey(nameof(AgentTypeId))]
    public AgentType AgentType { get; set; } = null!;

    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastSeenAt { get; set; }
}