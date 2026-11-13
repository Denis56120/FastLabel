namespace FastLabel.Backend.Models;

public class ResolutionDpi
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int Value { get; set; }
}