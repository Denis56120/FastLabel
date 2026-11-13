namespace FastLabel.Shared.DTOs;

public class ProtocolDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "protocols.error.name_required")]
    public string Name { get; set; } = string.Empty;
}