using System.ComponentModel.DataAnnotations;

namespace FastLabel.Backend.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "en"; // "fr" ou "en" pour le bilinguisme

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? PasswordResetToken { get; set; }
    public DateTime? ResetTokenExpires { get; set; }

    // Relations
    public Subscription? Subscription { get; set; }
    public ICollection<Agent> Agents { get; set; } = new List<Agent>();
    public ICollection<LabelTemplate> LabelTemplates { get; set; } = new List<LabelTemplate>();
}