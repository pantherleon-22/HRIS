using System.Security.Claims;
using Hris.Web.Data;
using Hris.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hris.Web.Controllers;

[Authorize(Roles = AccountController.Roles.Employee)]
public sealed class EmployeePortalController : Controller
{
    private readonly HrisDbContext _context;

    public EmployeePortalController(HrisDbContext context)
    {
        _context = context;
    }

    // GET: EmployeePortal
    public async Task<IActionResult> Index(DateTime? from)
    {
        var employeeId = GetEmployeeIdOrThrow();

        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee is null)
        {
            return Forbid();
        }

        var start = (from ?? DateTime.UtcNow.Date).Date;
        var to = start.AddDays(13);

        var assignments = await _context.ShiftAssignments
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.Shift.WorkDate >= start && a.Shift.WorkDate <= to)
            .Include(a => a.Shift)
            .ThenInclude(s => s.JobType)
            .OrderBy(a => a.Shift.WorkDate)
            .ToListAsync();

        assignments = assignments
            .OrderBy(a => a.Shift.WorkDate)
            .ThenBy(a => a.Shift.StartTime)
            .ToList();

        var days = assignments
            .GroupBy(a => a.Shift.WorkDate)
            .Select(g => new EmployeeScheduleDay(
                g.Key,
                g.OrderBy(a => a.Shift.StartTime)
                    .Select(a => new EmployeeScheduleItem(
                        a.ShiftId,
                        a.Shift.JobType.Name,
                        a.Shift.StartTime,
                        a.Shift.EndTime))
                    .ToList()))
            .ToList();

        var vm = new EmployeeScheduleVm(
            employee.Id,
            employee.FullName,
            start,
            to,
            assignments.Count,
            days);

        return View(vm);
    }

    // GET: EmployeePortal/OpenShifts
    public async Task<IActionResult> OpenShifts(DateTime? from)
    {
        var employeeId = GetEmployeeIdOrThrow();

        var start = (from ?? DateTime.UtcNow.Date).Date;
        var to = start.AddDays(13);

        var shifts = await _context.Shifts
            .AsNoTracking()
            .Where(s => s.Status == ShiftStatus.Open && s.WorkDate >= start && s.WorkDate <= to)
            .Include(s => s.JobType)
            .OrderBy(s => s.WorkDate)
            .ToListAsync();

        shifts = shifts
            .OrderBy(s => s.WorkDate)
            .ThenBy(s => s.StartTime)
            .ToList();

        var shiftIds = shifts.Select(s => s.Id).ToList();

        var assignedCounts = await _context.ShiftAssignments
            .AsNoTracking()
            .Where(a => shiftIds.Contains(a.ShiftId))
            .GroupBy(a => a.ShiftId)
            .Select(g => new { ShiftId = g.Key, Count = g.Count() })
            .ToListAsync();

        var assignedMap = assignedCounts.ToDictionary(x => x.ShiftId, x => x.Count);

        var myApplications = await _context.ShiftApplications
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && shiftIds.Contains(a.ShiftId))
            .Select(a => new { a.ShiftId, a.Status })
            .ToListAsync();

        var appMap = myApplications.ToDictionary(x => x.ShiftId, x => x.Status);

        var rows = shifts.Select(s =>
        {
            assignedMap.TryGetValue(s.Id, out var assigned);
            appMap.TryGetValue(s.Id, out var status);

            return new OpenShiftRow(
                s.Id,
                s.JobType.Name,
                s.WorkDate,
                s.StartTime,
                s.EndTime,
                s.Capacity,
                assigned,
                status);
        }).ToList();

        var vm = new OpenShiftsVm(start, to, rows);
        return View(vm);
    }

    // POST: EmployeePortal/Apply
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(int shiftId, DateTime? from)
    {
        var employeeId = GetEmployeeIdOrThrow();

        var shift = await _context.Shifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shiftId);

        if (shift is null || shift.Status != ShiftStatus.Open)
        {
            TempData["Toast"] = "Shift not available.";
            return RedirectToAction(nameof(OpenShifts), new { from });
        }

        var existing = await _context.ShiftApplications
            .AnyAsync(a => a.ShiftId == shiftId && a.EmployeeId == employeeId);

        if (existing)
        {
            TempData["Toast"] = "You already applied to this shift.";
            return RedirectToAction(nameof(OpenShifts), new { from });
        }

        var assignedCount = await _context.ShiftAssignments.CountAsync(a => a.ShiftId == shiftId);
        if (assignedCount >= shift.Capacity)
        {
            TempData["Toast"] = "This shift is full.";
            return RedirectToAction(nameof(OpenShifts), new { from });
        }

        _context.ShiftApplications.Add(new ShiftApplication
        {
            ShiftId = shiftId,
            EmployeeId = employeeId,
            Status = ShiftApplicationStatus.Pending,
            AppliedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync();
        TempData["Toast"] = "Application submitted.";
        return RedirectToAction(nameof(OpenShifts), new { from });
    }

    // GET: EmployeePortal/AskHr
    public async Task<IActionResult> AskHr()
    {
        var employeeId = GetEmployeeIdOrThrow();

        var messages = await _context.EmployeeMessages
            .AsNoTracking()
            .Where(m => m.EmployeeId == employeeId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToListAsync();

        var vm = new AskHrVm(new AskHrPost(), messages);
        return View(vm);
    }

    // POST: EmployeePortal/AskHr
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AskHr(AskHrPost post)
    {
        var employeeId = GetEmployeeIdOrThrow();

        if (!ModelState.IsValid)
        {
            var messages = await _context.EmployeeMessages
                .AsNoTracking()
                .Where(m => m.EmployeeId == employeeId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(20)
                .ToListAsync();

            return View(new AskHrVm(post, messages));
        }

        _context.EmployeeMessages.Add(new EmployeeMessage
        {
            EmployeeId = employeeId,
            Subject = post.Subject,
            Body = post.Body,
            CreatedAt = DateTime.UtcNow,
            Status = EmployeeMessageStatus.Open,
        });

        await _context.SaveChangesAsync();
        TempData["Toast"] = "Message sent to HR.";
        return RedirectToAction(nameof(AskHr));
    }

    private int GetEmployeeIdOrThrow()
    {
        var claim = User.FindFirst(AccountController.CustomClaims.EmployeeId)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(claim, out var employeeId))
        {
            throw new InvalidOperationException("Employee id claim missing.");
        }

        return employeeId;
    }

    public sealed record EmployeeScheduleVm(
        int EmployeeId,
        string EmployeeName,
        DateTime FromDate,
        DateTime ToDate,
        int TotalAssigned,
        IReadOnlyList<EmployeeScheduleDay> Days);

    public sealed record EmployeeScheduleDay(
        DateTime Date,
        IReadOnlyList<EmployeeScheduleItem> Items);

    public sealed record EmployeeScheduleItem(
        int ShiftId,
        string JobType,
        TimeSpan Start,
        TimeSpan End);

    public sealed record OpenShiftsVm(
        DateTime FromDate,
        DateTime ToDate,
        IReadOnlyList<OpenShiftRow> Rows);

    public sealed record OpenShiftRow(
        int ShiftId,
        string JobType,
        DateTime WorkDate,
        TimeSpan Start,
        TimeSpan End,
        int Capacity,
        int Assigned,
        ShiftApplicationStatus? MyStatus)
    {
        public int Remaining => Math.Max(Capacity - Assigned, 0);
        public bool IsFull => Remaining <= 0;
        public bool AlreadyApplied => MyStatus is ShiftApplicationStatus.Pending or ShiftApplicationStatus.Approved;
    }

    public sealed record AskHrVm(AskHrPost Post, IReadOnlyList<EmployeeMessage> Messages);

    public sealed class AskHrPost
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(120)]
        public string Subject { get; init; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(4000)]
        public string Body { get; init; } = string.Empty;
    }
}
