using System.ComponentModel.DataAnnotations;

namespace Hris.Web.Models;

public sealed class UserAccount
{
    public int Id { get; set; }

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Role { get; set; } = string.Empty; // "HR" | "Employee"

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(200)]
    public string? PasswordResetTokenHash { get; set; }

    public DateTime? PasswordResetTokenExpiresAt { get; set; }
}
