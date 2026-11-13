namespace FastLabel.Backend.Models;

public class LabelTemplate
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required]
    public string Name { get; set; } = string.Empty;

    public int BasicStatusId { get; set; }

    [ForeignKey(nameof(BasicStatusId))]
    public BasicStatus BasicStatus { get; set; } = null!;

    [Required]
    public string JsonData { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}