using System.ComponentModel.DataAnnotations;

namespace Hris.Web.Models;

public enum EmployeeMessageStatus
{
    Open = 1,
    Answered = 2,
    Closed = 3
}

public sealed class EmployeeMessage
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [Required]
    [StringLength(120)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(4000)]
    public string Body { get; set; } = string.Empty;

    public EmployeeMessageStatus Status { get; set; } = EmployeeMessageStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(4000)]
    public string? HrReply { get; set; }

    public DateTime? RepliedAt { get; set; }

    public DateTime? ClosedAt { get; set; }
}
