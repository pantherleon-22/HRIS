using System.Diagnostics;
using Hris.Web.Data;
using Hris.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hris.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly HrisDbContext _context;

    public HomeController(ILogger<HomeController> logger, HrisDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole(AccountController.Roles.Employee))
        {
            return RedirectToAction("Index", "EmployeePortal");
        }

        var dashboardData = new
        {
            TotalEmployees = await _context.Employees.CountAsync(),
            OpenShifts = await _context.Shifts.CountAsync(s => s.Status == ShiftStatus.Open),
            PendingApplications = await _context.ShiftApplications.CountAsync(sa => sa.Status == ShiftApplicationStatus.Pending),
            ActiveDepartments = await _context.Departments.CountAsync()
        };

        return View(dashboardData);
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
