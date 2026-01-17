using Hris.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hris.Web.Controllers;

[Authorize(Roles = AccountController.Roles.HrOrAdmin)]
public sealed class DepartmentsController : Controller
{
    private readonly HrisDbContext _context;

    public DepartmentsController(HrisDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var departments = await _context.Departments
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentIndexRow(
                d.Id,
                d.Name,
                _context.Employees.Count(e => e.DepartmentId == d.Id)))
            .ToListAsync();

        return View(departments);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id.Value);

        if (department is null)
        {
            return NotFound();
        }

        var employees = await _context.Employees
            .AsNoTracking()
            .Where(e => e.DepartmentId == department.Id)
            .Include(e => e.Title)
            .OrderBy(e => e.Title.Name)
            .ThenBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync();

        var byTitle = employees
            .GroupBy(e => e.Title.Name)
            .Select(g => new DepartmentTitleGroup(g.Key, g.Count(), g.ToList()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Title)
            .ToList();

        var vm = new DepartmentDetailsVm(
            department.Id,
            department.Name,
            employees.Count,
            byTitle);

        return View(vm);
    }

    public sealed record DepartmentIndexRow(int Id, string Name, int EmployeeCount);

    public sealed record DepartmentDetailsVm(
        int Id,
        string Name,
        int EmployeeCount,
        IReadOnlyList<DepartmentTitleGroup> TitleGroups);

    public sealed record DepartmentTitleGroup(
        string Title,
        int Count,
        IReadOnlyList<Models.Employee> Employees);
}
