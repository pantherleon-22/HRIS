using System.ComponentModel.DataAnnotations;

namespace Hris.Web.Models;

public enum DisciplinaryActionType
{
    Warning = 1,
    Penalty = 2
}

public sealed class DisciplinaryAction
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [Required]
    public DisciplinaryActionType Type { get; set; }

    [Required, DataType(DataType.Date)]
    public DateTime OccurredOn { get; set; } = DateTime.UtcNow.Date;

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Range(0, 1000)]
    public int Points { get; set; }
}
