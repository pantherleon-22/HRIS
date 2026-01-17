using System.ComponentModel.DataAnnotations;

namespace Hris.Web.Models;

public enum ShiftApplicationStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public sealed class ShiftApplication
{
    public int Id { get; set; }

    public int ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [Required]
    public ShiftApplicationStatus Status { get; set; } = ShiftApplicationStatus.Pending;

    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DecidedAt { get; set; }

    [StringLength(500)]
    public string? DecisionNote { get; set; }
}
