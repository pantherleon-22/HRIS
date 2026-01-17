using System.ComponentModel.DataAnnotations;

namespace Hris.Web.Models;

public sealed class Availability
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [Required, DataType(DataType.Date)]
    public DateTime WorkDate { get; set; } = DateTime.UtcNow.Date;

    public bool IsAvailable { get; set; } = true;

    public TimeSpan? AvailableFrom { get; set; }

    public TimeSpan? AvailableTo { get; set; }
}
