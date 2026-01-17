using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Hris.Web.Data;
using Hris.Web.Models;

namespace Hris.Web.Controllers
{
    [Authorize(Roles = AccountController.Roles.HrOrAdmin)]
    public class EmployeesController : Controller
    {
        private readonly HrisDbContext _context;

        public EmployeesController(HrisDbContext context)
        {
            _context = context;
        }

        // GET: Employees
        public async Task<IActionResult> Index()
        {
            var hrisDbContext = _context.Employees.Include(e => e.Department).Include(e => e.Title);
            return View(await hrisDbContext.ToListAsync());
        }

        // GET: Employees/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Title)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        // GET: Employees/Schedule/5
        public async Task<IActionResult> Schedule(int? id, DateTime? from)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            var start = (from ?? DateTime.UtcNow.Date).Date;
            var to = start.AddDays(13);

            var assignments = await _context.ShiftAssignments
                .AsNoTracking()
                .Where(a => a.EmployeeId == employee.Id && a.Shift.WorkDate >= start && a.Shift.WorkDate <= to)
                .Include(a => a.Shift)
                .ThenInclude(s => s.JobType)
                .OrderBy(a => a.Shift.WorkDate)
                .ToListAsync();

            // SQLite EF provider can't translate TimeSpan ORDER BY reliably; order on client.
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
                            a.Shift.EndTime)).ToList()))
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

        // GET: Employees/Create
        public IActionResult Create()
        {
            ViewData["DepartmentId"] = new SelectList(_context.Departments, "Id", "Name");
            ViewData["TitleId"] = new SelectList(_context.Titles, "Id", "Name");
            return View();
        }

        // POST: Employees/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,FirstName,LastName,Email,Phone,HireDate,ExternalExperienceYears,DepartmentId,TitleId")] Employee employee)
        {
            if (ModelState.IsValid)
            {
                _context.Add(employee);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["DepartmentId"] = new SelectList(_context.Departments, "Id", "Name", employee.DepartmentId);
            ViewData["TitleId"] = new SelectList(_context.Titles, "Id", "Name", employee.TitleId);
            return View(employee);
        }

        // GET: Employees/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }
            ViewData["DepartmentId"] = new SelectList(_context.Departments, "Id", "Name", employee.DepartmentId);
            ViewData["TitleId"] = new SelectList(_context.Titles, "Id", "Name", employee.TitleId);
            return View(employee);
        }

        // POST: Employees/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FirstName,LastName,Email,Phone,HireDate,ExternalExperienceYears,DepartmentId,TitleId")] Employee employee)
        {
            if (id != employee.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(employee);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeExists(employee.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["DepartmentId"] = new SelectList(_context.Departments, "Id", "Name", employee.DepartmentId);
            ViewData["TitleId"] = new SelectList(_context.Titles, "Id", "Name", employee.TitleId);
            return View(employee);
        }

        // GET: Employees/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Title)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        // POST: Employees/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null)
            {
                _context.Employees.Remove(employee);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EmployeeExists(int id)
        {
            return _context.Employees.Any(e => e.Id == id);
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
    }
}
