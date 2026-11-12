namespace FastLabel.Shared.DTOs;

public class PrinterDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string IP { get; set; } = string.Empty;
    public int Port { get; set; }
    public string ProtocolName { get; set; } = string.Empty;       
    public string ResolutionDpiName { get; set; } = string.Empty; 
    public string StatusName { get; set; } = string.Empty;        
    public DateTime CreatedAt { get; set; }
}