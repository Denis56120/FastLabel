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

    public bool IsAdmin { get; set; } = false;

    public string? PasswordResetToken { get; set; }

    public DateTime? ResetTokenExpired { get; set; }

    // Ajouté pour la vérification et l'activation du compte par e-mail
    public string? EmailVerificationToken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int BasicStatusId { get; set; }

    [ForeignKey(nameof(BasicStatusId))]
    public BasicStatus BasicStatus { get; set; } = null!;
}