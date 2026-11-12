namespace FastLabel.Shared.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string BasicStatusName { get; set; } = string.Empty; 
    public DateTime CreatedAt { get; set; }
}