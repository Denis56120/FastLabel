namespace FastLabel.Backend.Models;

public class BasicStatus
{
    [Key]
    public int Id { get; set; }

    public int NameTextId { get; set; }

    [ForeignKey(nameof(NameTextId))]
    public Text Name { get; set; } = null!;
}