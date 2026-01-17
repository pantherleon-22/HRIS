using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Hris.Web.Models;

public sealed class Employee
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required, DataType(DataType.Date)]
    public DateTime HireDate { get; set; } = DateTime.UtcNow.Date;

    [Range(0, 60)]
    public decimal ExternalExperienceYears { get; set; }

    public int DepartmentId { get; set; }

    [ValidateNever]
    public Department Department { get; set; } = null!;

    public int TitleId { get; set; }

    [ValidateNever]
    public Title Title { get; set; } = null!;

    [NotMapped]
    public decimal CompanyExperienceYears
        => (decimal)((DateTime.UtcNow.Date - HireDate.Date).TotalDays / 365.25);

    [NotMapped]
    public decimal TotalExperienceYears => ExternalExperienceYears + CompanyExperienceYears;

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
}
