using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Hris.Web.Models;

public sealed class ShiftAssignment
{
    public int Id { get; set; }

    [Required]
    public int ShiftId { get; set; }

    [ValidateNever]
    public Shift Shift { get; set; } = null!;

    [Required]
    public int EmployeeId { get; set; }

    [ValidateNever]
    public Employee Employee { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
