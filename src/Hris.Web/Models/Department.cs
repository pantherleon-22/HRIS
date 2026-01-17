using System.ComponentModel.DataAnnotations;

namespace Hris.Web.Models;

public sealed class Department
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
}
