using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Models;

[Index(nameof(Email), IsUnique = true)]
public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public int PreferredLanguageId { get; set; }

    [ForeignKey(nameof(PreferredLanguageId))]
    public PreferredLanguage PreferredLanguage { get; set; } = null!;

    public string? PasswordResetToken { get; set; }

    public DateTime? ResetTokenExpired { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int BasicStatusId { get; set; }

    [ForeignKey(nameof(BasicStatusId))]
    public BasicStatus BasicStatus { get; set; } = null!;
}