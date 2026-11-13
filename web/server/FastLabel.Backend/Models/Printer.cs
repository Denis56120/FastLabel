namespace FastLabel.Backend.Models;

public class Printer
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Location { get; set; } = string.Empty;

    [Required]
    public string IP { get; set; } = string.Empty;

    [Required]
    public int Port { get; set; }

    public int ProtocolId { get; set; }

    [ForeignKey(nameof(ProtocolId))]
    public Protocol Protocol { get; set; } = null!;

    public int ResolutionDpiId { get; set; }

    [ForeignKey(nameof(ResolutionDpiId))]
    public ResolutionDpi ResolutionDpi { get; set; } = null!;

    public int BasicStatusId { get; set; }

    [ForeignKey(nameof(BasicStatusId))]
    public BasicStatus BasicStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}