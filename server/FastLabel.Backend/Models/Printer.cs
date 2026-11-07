namespace FastLabel.Backend.Models;

public class Printer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public int Port { get; set; } = 9100;
    public string Protocol { get; set; } = "ZPL"; 
    public int ResolutionDpi { get; set; } = 203; 
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Propriété de navigation EF Core (optionnelle)
    public Agent? Agent { get; set; }
}