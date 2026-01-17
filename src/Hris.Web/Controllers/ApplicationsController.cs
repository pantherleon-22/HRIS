using Hris.Web.Data;
using Hris.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hris.Web.Controllers;

[Authorize(Roles = AccountController.Roles.HrOrAdmin)]
public sealed class ApplicationsController : Controller
{
    private readonly HrisDbContext _context;

    public ApplicationsController(HrisDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? shiftId = null)
    {
        var query = _context.ShiftApplications
            .AsNoTracking()
            .Where(a => a.Status == ShiftApplicationStatus.Pending)
            .Include(a => a.Employee)
            .Include(a => a.Shift!)
            .ThenInclude(s => s.JobType)
            .Where(a => a.Employee != null && a.Shift != null)
            .OrderByDescending(a => a.AppliedAt)
            .AsQueryable();

        if (shiftId != null)
        {
            query = query.Where(a => a.ShiftId == shiftId.Value);
        }

        var rows = await query
            .Select(a => new PendingApplicationRow(
                a.Id,
                a.ShiftId,
                a.Shift!.JobType!.Name,
                a.Shift!.WorkDate,
                a.Shift!.StartTime,
                a.Shift!.EndTime,
                a.EmployeeId,
                a.Employee!.FirstName,
                a.Employee!.LastName,
                a.Employee!.Email,
                a.AppliedAt))
            .ToListAsync();

        return View(new PendingApplicationsVm(shiftId, rows));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? note = null)
    {
        var app = await _context.ShiftApplications
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (app is null)
        {
            return NotFound();
        }

        if (app.Status != ShiftApplicationStatus.Pending)
        {
            TempData["Toast"] = "Application already decided.";
            return RedirectToAction(nameof(Index));
        }

        var assignedCount = await _context.ShiftAssignments.CountAsync(sa => sa.ShiftId == app.ShiftId);
        if (assignedCount >= app.Shift!.Capacity)
        {
            TempData["Toast"] = "Shift capacity is full.";
            return RedirectToAction(nameof(Index), new { shiftId = app.ShiftId });
        }

        var duplicate = await _context.ShiftAssignments.AnyAsync(sa => sa.ShiftId == app.ShiftId && sa.EmployeeId == app.EmployeeId);
        if (!duplicate)
        {
            _context.ShiftAssignments.Add(new ShiftAssignment
            {
                ShiftId = app.ShiftId,
                EmployeeId = app.EmployeeId,
            });
        }

        app.Status = ShiftApplicationStatus.Approved;
        app.DecidedAt = DateTime.UtcNow;
        app.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note;

        await _context.SaveChangesAsync();

        TempData["Toast"] = "Application approved and assigned.";
        return RedirectToAction(nameof(Index), new { shiftId = app.ShiftId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? note = null)
    {
        var app = await _context.ShiftApplications.FirstOrDefaultAsync(a => a.Id == id);
        if (app is null)
        {
            return NotFound();
        }

        if (app.Status != ShiftApplicationStatus.Pending)
        {
            TempData["Toast"] = "Application already decided.";
            return RedirectToAction(nameof(Index));
        }

        app.Status = ShiftApplicationStatus.Rejected;
        app.DecidedAt = DateTime.UtcNow;
        app.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note;

        await _context.SaveChangesAsync();

        TempData["Toast"] = "Application rejected.";
        return RedirectToAction(nameof(Index), new { shiftId = app.ShiftId });
    }

    public sealed record PendingApplicationsVm(int? ShiftId, IReadOnlyList<PendingApplicationRow> Rows);

    public sealed record PendingApplicationRow(
        int ApplicationId,
        int ShiftId,
        string JobType,
        DateTime WorkDate,
        TimeSpan Start,
        TimeSpan End,
        int EmployeeId,
        string FirstName,
        string LastName,
        string Email,
        DateTime AppliedAt)
    {
        public string EmployeeName => $"{FirstName} {LastName}";
    }
}
