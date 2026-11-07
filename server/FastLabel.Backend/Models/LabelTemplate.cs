namespace FastLabel.Backend.Models;

public class LabelTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    // Le contenu JSON définissant les champs, dimensions, variables, ou layout ZPL
    public string JsonData { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}