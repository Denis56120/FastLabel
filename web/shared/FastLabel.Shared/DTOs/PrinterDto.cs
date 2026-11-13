using System.ComponentModel.DataAnnotations;

namespace FastLabel.Shared.DTOs;

public class PrinterDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "printer.error.name_required")]
    [StringLength(100, ErrorMessage = "printer.error.name_max_length")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "printer.error.location_required")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "printer.error.ip_required")]
    public string IP { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "printer.error.port_invalid")]
    public int Port { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "printer.error.protocol_required")]
    public int ProtocolId { get; set; }
    public string ProtocolName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "printer.error.resolution_required")]
    public int ResolutionDpiId { get; set; }
    public string ResolutionDpiName { get; set; } = string.Empty;

    public int BasicStatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}