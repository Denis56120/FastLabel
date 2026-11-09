namespace FastLabel.Shared.DTOs;

public class PrintRequest
{
    public string ApiKey { get; set; } = string.Empty;
    public int PrinterId { get; set; }
    public int? TemplateId { get; set; }
    public int Quantity { get; set; }
    public string? RawData { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
}