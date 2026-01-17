using System.ComponentModel.DataAnnotations;

namespace Hris.Web.Models;

public sealed class JobType
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}

public sealed class JobTypeAllowedTitle
{
    public int JobTypeId { get; set; }
    public JobType? JobType { get; set; }

    public int TitleId { get; set; }
    public Title? Title { get; set; }
}
