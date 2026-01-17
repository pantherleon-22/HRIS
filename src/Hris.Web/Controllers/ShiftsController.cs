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
using System.ComponentModel.DataAnnotations;

namespace Hris.Web.Controllers
{
    [Authorize(Roles = AccountController.Roles.HrOrAdmin)]
    public class ShiftsController : Controller
    {
        private readonly HrisDbContext _context;

        public ShiftsController(HrisDbContext context)
        {
            _context = context;
        }

        // GET: Shifts
        public async Task<IActionResult> Index()
        {
            var hrisDbContext = _context.Shifts.Include(s => s.JobType);
            return View(await hrisDbContext.ToListAsync());
        }

        // GET: Shifts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shift = await _context.Shifts
                .Include(s => s.JobType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (shift == null)
            {
                return NotFound();
            }

            return View(shift);
        }

        // GET: Shifts/Assign/5
        public async Task<IActionResult> Assign(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shift = await _context.Shifts
                .AsNoTracking()
                .Include(s => s.JobType)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shift == null)
            {
                return NotFound();
            }

            var assignedEmployees = await _context.ShiftAssignments
                .AsNoTracking()
                .Where(a => a.ShiftId == shift.Id)
                .Include(a => a.Employee)
                .OrderBy(a => a.Employee.LastName)
                .ThenBy(a => a.Employee.FirstName)
                .Select(a => new AssignedEmployeeRow(a.EmployeeId, a.Employee.FirstName, a.Employee.LastName, a.Employee.Email))
                .ToListAsync();

            var remaining = Math.Max(shift.Capacity - assignedEmployees.Count, 0);

            var employees = await _context.Employees
                .AsNoTracking()
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new EmployeeOption(e.Id, e.FirstName, e.LastName, e.Email))
                .ToListAsync();

            var vm = new AssignShiftVm(
                shift.Id,
                shift.JobType.Name,
                shift.WorkDate,
                shift.StartTime,
                shift.EndTime,
                shift.Capacity,
                assignedEmployees.Count,
                remaining,
                null,
                employees,
                assignedEmployees);

            return View(vm);
        }

        // POST: Shifts/Assign/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int id, AssignShiftPost post)
        {
            var shift = await _context.Shifts
                .Include(s => s.JobType)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shift == null)
            {
                return NotFound();
            }

            if (post.EmployeeId == null)
            {
                ModelState.AddModelError(nameof(post.EmployeeId), "Employee is required.");
            }

            var assignedCount = await _context.ShiftAssignments.CountAsync(a => a.ShiftId == shift.Id);
            if (assignedCount >= shift.Capacity)
            {
                ModelState.AddModelError(string.Empty, "Shift capacity is full.");
            }

            if (post.EmployeeId != null)
            {
                var duplicate = await _context.ShiftAssignments
                    .AnyAsync(a => a.ShiftId == shift.Id && a.EmployeeId == post.EmployeeId.Value);

                if (duplicate)
                {
                    ModelState.AddModelError(string.Empty, "This employee is already assigned to the shift.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.ShiftAssignments.Add(new ShiftAssignment
                {
                    ShiftId = shift.Id,
                    EmployeeId = post.EmployeeId!.Value,
                });

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Details), new { id = shift.Id });
            }

            // Rebuild VM for redisplay
            var assignedEmployees = await _context.ShiftAssignments
                .AsNoTracking()
                .Where(a => a.ShiftId == shift.Id)
                .Include(a => a.Employee)
                .OrderBy(a => a.Employee.LastName)
                .ThenBy(a => a.Employee.FirstName)
                .Select(a => new AssignedEmployeeRow(a.EmployeeId, a.Employee.FirstName, a.Employee.LastName, a.Employee.Email))
                .ToListAsync();

            var remaining = Math.Max(shift.Capacity - assignedEmployees.Count, 0);

            var employees = await _context.Employees
                .AsNoTracking()
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new EmployeeOption(e.Id, e.FirstName, e.LastName, e.Email))
                .ToListAsync();

            var vm = new AssignShiftVm(
                shift.Id,
                shift.JobType.Name,
                shift.WorkDate,
                shift.StartTime,
                shift.EndTime,
                shift.Capacity,
                assignedEmployees.Count,
                remaining,
                post.EmployeeId,
                employees,
                assignedEmployees);

            return View(vm);
        }

        // GET: Shifts/Create
        public IActionResult Create()
        {
            ViewData["JobTypeId"] = new SelectList(_context.JobTypes, "Id", "Name");
            return View();
        }

        // GET: Shifts/GenerateTwoWeek
        public async Task<IActionResult> GenerateTwoWeek()
        {
            var vm = await BuildGenerateVmAsync(new GenerateTwoWeekInput());
            return View(vm);
        }

        // POST: Shifts/GenerateTwoWeek
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateTwoWeek(GenerateTwoWeekInput input)
        {
            if (input.StartDate == default)
            {
                ModelState.AddModelError(nameof(input.StartDate), "Start date is required.");
            }

            if (input.JobTypeId == null)
            {
                ModelState.AddModelError(nameof(input.JobTypeId), "Job type is required.");
            }

            // Basic per-day validation
            foreach (var day in input.Days)
            {
                if (!day.Enabled)
                {
                    continue;
                }

                if (day.End <= day.Start)
                {
                    ModelState.AddModelError(string.Empty, $"End time must be after start time for {day.Label}.");
                }
            }

            if (input.Days.All(d => !d.Enabled))
            {
                ModelState.AddModelError(string.Empty, "Please enable at least one day.");
            }

            var created = 0;
            var skipped = 0;
            var errors = 0;
            var createdShifts = new List<Shift>();

            if (ModelState.IsValid)
            {
                var start = input.StartDate.Date;
                var end = start.AddDays(13);

                var enabledMap = input.Days
                    .Where(d => d.Enabled)
                    .ToDictionary(d => d.DayOfWeek, d => d);

                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    if (!enabledMap.TryGetValue(date.DayOfWeek, out var rule))
                    {
                        continue;
                    }

                    try
                    {
                        var exists = await _context.Shifts.AnyAsync(s =>
                            s.JobTypeId == input.JobTypeId!.Value &&
                            s.WorkDate == date &&
                            s.StartTime == rule.Start &&
                            s.EndTime == rule.End);

                        if (exists && input.SkipIfExists)
                        {
                            skipped++;
                            continue;
                        }

                        if (exists && !input.SkipIfExists)
                        {
                            errors++;
                            continue;
                        }

                        var newShift = new Shift
                        {
                            JobTypeId = input.JobTypeId!.Value,
                            WorkDate = date,
                            StartTime = rule.Start,
                            EndTime = rule.End,
                            Capacity = input.Capacity,
                            Status = input.Status,
                        };

                        _context.Shifts.Add(newShift);
                        createdShifts.Add(newShift);

                        created++;
                    }
                    catch
                    {
                        errors++;
                    }
                }

                if (created > 0)
                {
                    await _context.SaveChangesAsync();

                    if (input.EmployeeId != null)
                    {
                        foreach (var shift in createdShifts)
                        {
                            // Safety: avoid duplicate assignment.
                            var alreadyAssigned = await _context.ShiftAssignments
                                .AnyAsync(a => a.ShiftId == shift.Id && a.EmployeeId == input.EmployeeId.Value);

                            if (alreadyAssigned)
                            {
                                continue;
                            }

                            _context.ShiftAssignments.Add(new ShiftAssignment
                            {
                                ShiftId = shift.Id,
                                EmployeeId = input.EmployeeId.Value,
                            });
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                if (created == 0 && skipped == 0 && errors == 0)
                {
                    // Should not usually happen, but ensures the UI can explain why nothing changed.
                    skipped = 0;
                }
            }

            var vm = await BuildGenerateVmAsync(input, created, skipped, errors);
            return View(vm);
        }

        // POST: Shifts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,JobTypeId,WorkDate,StartTime,EndTime,Capacity,Status")] Shift shift)
        {
            if (ModelState.IsValid)
            {
                _context.Add(shift);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["JobTypeId"] = new SelectList(_context.JobTypes, "Id", "Name", shift.JobTypeId);
            return View(shift);
        }

        // GET: Shifts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shift = await _context.Shifts.FindAsync(id);
            if (shift == null)
            {
                return NotFound();
            }
            ViewData["JobTypeId"] = new SelectList(_context.JobTypes, "Id", "Name", shift.JobTypeId);
            return View(shift);
        }

        // POST: Shifts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,JobTypeId,WorkDate,StartTime,EndTime,Capacity,Status")] Shift shift)
        {
            if (id != shift.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(shift);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShiftExists(shift.Id))
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
            ViewData["JobTypeId"] = new SelectList(_context.JobTypes, "Id", "Name", shift.JobTypeId);
            return View(shift);
        }

        // GET: Shifts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shift = await _context.Shifts
                .Include(s => s.JobType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (shift == null)
            {
                return NotFound();
            }

            return View(shift);
        }

        // POST: Shifts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift != null)
            {
                _context.Shifts.Remove(shift);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ShiftExists(int id)
        {
            return _context.Shifts.Any(e => e.Id == id);
        }

        public sealed record AssignShiftPost(int? EmployeeId);

        public sealed record AssignShiftVm(
            int ShiftId,
            string JobType,
            DateTime WorkDate,
            TimeSpan StartTime,
            TimeSpan EndTime,
            int Capacity,
            int AssignedCount,
            int Remaining,
            int? SelectedEmployeeId,
            IReadOnlyList<EmployeeOption> Employees,
            IReadOnlyList<AssignedEmployeeRow> AssignedEmployees);

        public sealed record EmployeeOption(int Id, string FirstName, string LastName, string Email)
        {
            public string DisplayName => $"{FirstName} {LastName} ({Email})";
        }

        public sealed record AssignedEmployeeRow(int EmployeeId, string FirstName, string LastName, string Email)
        {
            public string DisplayName => $"{FirstName} {LastName}";
        }

        public sealed class GenerateTwoWeekVm
        {
            public DateTime StartDate { get; init; }
            public int? JobTypeId { get; init; }
            public SelectList JobTypes { get; init; } = null!;
            public int? EmployeeId { get; init; }
            public SelectList Employees { get; init; } = null!;
            public int Capacity { get; init; } = 1;
            public ShiftStatus Status { get; init; } = ShiftStatus.Open;
            public bool SkipIfExists { get; init; } = true;
            public List<GenerateDayRule> Days { get; init; } = new();

            public int CreatedCount { get; init; }
            public int SkippedCount { get; init; }
            public int ErrorCount { get; init; }
        }

        public sealed class GenerateTwoWeekInput
        {
            [DataType(DataType.Date)]
            public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

            public int? JobTypeId { get; set; }

            public int? EmployeeId { get; set; }

            [Range(1, 500)]
            public int Capacity { get; set; } = 1;

            public ShiftStatus Status { get; set; } = ShiftStatus.Open;

            public bool SkipIfExists { get; set; } = true;

            public List<GenerateDayRule> Days { get; set; } = new();
        }

        public sealed class GenerateDayRule
        {
            public DayOfWeek DayOfWeek { get; set; }
            public string Label { get; set; } = string.Empty;
            public bool Enabled { get; set; }
            public TimeSpan Start { get; set; } = new(9, 0, 0);
            public TimeSpan End { get; set; } = new(18, 0, 0);
        }

        private async Task<GenerateTwoWeekVm> BuildGenerateVmAsync(GenerateTwoWeekInput input, int created = 0, int skipped = 0, int errors = 0)
        {
            var jobTypes = await _context.JobTypes.AsNoTracking().OrderBy(j => j.Name).ToListAsync();
            var employees = await _context.Employees
                .AsNoTracking()
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new { e.Id, Text = e.FirstName + " " + e.LastName + " (" + e.Email + ")" })
                .ToListAsync();
            var days = input.Days;

            if (days.Count == 0)
            {
                days = new List<GenerateDayRule>
                {
                    new() { DayOfWeek = DayOfWeek.Monday, Label = "Monday", Enabled = true },
                    new() { DayOfWeek = DayOfWeek.Tuesday, Label = "Tuesday", Enabled = true },
                    new() { DayOfWeek = DayOfWeek.Wednesday, Label = "Wednesday", Enabled = true },
                    new() { DayOfWeek = DayOfWeek.Thursday, Label = "Thursday", Enabled = true },
                    new() { DayOfWeek = DayOfWeek.Friday, Label = "Friday", Enabled = true },
                    new() { DayOfWeek = DayOfWeek.Saturday, Label = "Saturday", Enabled = false },
                    new() { DayOfWeek = DayOfWeek.Sunday, Label = "Sunday", Enabled = false },
                };
            }

            return new GenerateTwoWeekVm
            {
                StartDate = input.StartDate == default ? DateTime.UtcNow.Date : input.StartDate.Date,
                JobTypeId = input.JobTypeId,
                JobTypes = new SelectList(jobTypes, "Id", "Name", input.JobTypeId),
                EmployeeId = input.EmployeeId,
                Employees = new SelectList(employees, "Id", "Text", input.EmployeeId),
                Capacity = input.Capacity,
                Status = input.Status,
                SkipIfExists = input.SkipIfExists,
                Days = days,
                CreatedCount = created,
                SkippedCount = skipped,
                ErrorCount = errors,
            };
        }
    }
}
