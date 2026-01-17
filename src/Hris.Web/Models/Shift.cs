using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Hris.Web.Models;

public enum ShiftStatus
{
    Open = 1,
    Closed = 2,
    Cancelled = 3
}

public sealed class Shift
{
    public int Id { get; set; }

    public int JobTypeId { get; set; }

    [ValidateNever]
    public JobType JobType { get; set; } = null!;

    [Required, DataType(DataType.Date)]
    public DateTime WorkDate { get; set; } = DateTime.UtcNow.Date;

    [Required]
    public TimeSpan StartTime { get; set; } = new(9, 0, 0);

    [Required]
    public TimeSpan EndTime { get; set; } = new(18, 0, 0);

    [Range(1, 500)]
    public int Capacity { get; set; } = 1;

    [Required]
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
}
