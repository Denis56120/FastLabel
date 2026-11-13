namespace FastLabel.Backend.Models;

public class SubscriptionType
{
    [Key]
    public int Id { get; set; }

    public int NameTextId { get; set; }

    [ForeignKey(nameof(NameTextId))]
    public Text Name { get; set; } = null!;
}