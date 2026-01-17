using Hris.Web.Data;
using Hris.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hris.Web.Controllers;

[Authorize(Roles = AccountController.Roles.HrOrAdmin)]
public sealed class HrMessagesController : Controller
{
    private readonly HrisDbContext _context;

    public HrMessagesController(HrisDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var rows = await _context.EmployeeMessages
            .AsNoTracking()
            .Include(m => m.Employee)
            .Where(m => m.Employee != null)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MessageRow(
                m.Id,
                m.EmployeeId,
                m.Employee!.FirstName,
                m.Employee!.LastName,
                m.Employee!.Email,
                m.Subject,
                m.Status,
                m.CreatedAt,
                m.RepliedAt,
                m.ClosedAt))
            .ToListAsync();

        return View(rows);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var msg = await _context.EmployeeMessages
            .AsNoTracking()
            .Include(m => m.Employee)
            .FirstOrDefaultAsync(m => m.Id == id.Value);

        if (msg is null)
        {
            return NotFound();
        }

        return View(msg);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int id, string reply)
    {
        var msg = await _context.EmployeeMessages.FirstOrDefaultAsync(m => m.Id == id);
        if (msg is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            TempData["Toast"] = "Reply cannot be empty.";
            return RedirectToAction(nameof(Details), new { id });
        }

        msg.HrReply = reply;
        msg.RepliedAt = DateTime.UtcNow;
        msg.Status = EmployeeMessageStatus.Answered;

        await _context.SaveChangesAsync();

        TempData["Toast"] = "Reply sent.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        var msg = await _context.EmployeeMessages.FirstOrDefaultAsync(m => m.Id == id);
        if (msg is null)
        {
            return NotFound();
        }

        msg.Status = EmployeeMessageStatus.Closed;
        msg.ClosedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Toast"] = "Message closed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public sealed record MessageRow(
        int Id,
        int EmployeeId,
        string FirstName,
        string LastName,
        string Email,
        string Subject,
        EmployeeMessageStatus Status,
        DateTime CreatedAt,
        DateTime? RepliedAt,
        DateTime? ClosedAt)
    {
        public string EmployeeName => $"{FirstName} {LastName}";
    }
}
