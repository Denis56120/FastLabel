namespace FastLabel.Shared.DTOs;

public class UpdatePreferencesDto
{
    public bool ReceiveEmailNotifications { get; set; }
    public string? DefaultPrinterName { get; set; }
}