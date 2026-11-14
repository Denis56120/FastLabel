namespace FastLabel.Backend.Models;

public class Plan
{
    [Key]
    public int Id { get; set; }

    public int NameTextId { get; set; }

    [ForeignKey(nameof(NameTextId))]
    public Text Name { get; set; } = null!;

    public int DescriptionJsonTextId { get; set; }

    [ForeignKey(nameof(DescriptionJsonTextId))]
    public Text DescriptionJson { get; set; } = null!;

    public int BasicStatusId { get; set; }

    [ForeignKey(nameof(BasicStatusId))]
    public BasicStatus BasicStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}