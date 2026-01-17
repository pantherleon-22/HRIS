using Hris.Web.Data;
using Hris.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hris.Web.Controllers;

[Authorize(Roles = AccountController.Roles.Admin)]
public sealed class AdminController : Controller
{
    private readonly HrisDbContext _context;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;

    public AdminController(HrisDbContext context, IPasswordHasher<UserAccount> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var accounts = await _context.UserAccounts
            .AsNoTracking()
            .Include(a => a.Employee)
            .OrderBy(a => a.Role)
            .ThenBy(a => a.Email)
            .Select(a => new AccountRow
            {
                Id = a.Id,
                Email = a.Email,
                Role = a.Role,
                EmployeeId = a.EmployeeId,
                EmployeeName = a.Employee == null ? null : (a.Employee.FirstName + " " + a.Employee.LastName)
            })
            .ToListAsync();

        var employeesWithoutAccount = await _context.Employees
            .AsNoTracking()
            .Where(e => !_context.UserAccounts.Any(a => a.EmployeeId == e.Id))
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Select(e => new EmployeeRow
            {
                Id = e.Id,
                FullName = e.FirstName + " " + e.LastName,
                Email = e.Email
            })
            .ToListAsync();

        return View(new AdminIndexVm
        {
            Accounts = accounts,
            EmployeesWithoutAccount = employeesWithoutAccount
        });
    }

    [HttpGet]
    public IActionResult CreateHr() => View(new CreateHrVm());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateHr(CreateHrPost post)
    {
        if (string.IsNullOrWhiteSpace(post.Email))
        {
            ModelState.AddModelError(nameof(post.Email), "Email is required.");
        }

        if (string.IsNullOrWhiteSpace(post.Password) || post.Password.Length < 8)
        {
            ModelState.AddModelError(nameof(post.Password), "Password must be at least 8 characters.");
        }

        if (!ModelState.IsValid)
        {
            return View(new CreateHrVm { Email = post.Email });
        }

        var email = post.Email!.Trim();
        var exists = await _context.UserAccounts.AnyAsync(a => a.Email == email);
        if (exists)
        {
            ModelState.AddModelError(nameof(post.Email), "This email already has an account.");
            return View(new CreateHrVm { Email = post.Email });
        }

        var account = new UserAccount { Email = email, Role = AccountController.Roles.Hr };
        account.PasswordHash = _passwordHasher.HashPassword(account, post.Password!);
        _context.UserAccounts.Add(account);
        await _context.SaveChangesAsync();

        TempData["Toast"] = "Yeni HR hesabı oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateEmployeeAccount(int id)
    {
        var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (emp is null) return NotFound();

        var hasAccount = await _context.UserAccounts.AnyAsync(a => a.EmployeeId == emp.Id);
        if (hasAccount)
        {
            TempData["Toast"] = "Bu çalışan için zaten bir hesap var.";
            return RedirectToAction(nameof(Index));
        }

        return View(new CreateEmployeeAccountVm
        {
            EmployeeId = emp.Id,
            EmployeeName = emp.FirstName + " " + emp.LastName,
            Email = emp.Email
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployeeAccount(CreateEmployeeAccountPost post)
    {
        if (post.EmployeeId <= 0)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(post.Password) || post.Password.Length < 8)
        {
            ModelState.AddModelError(nameof(post.Password), "Password must be at least 8 characters.");
        }

        var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == post.EmployeeId);
        if (emp is null)
        {
            return NotFound();
        }

        var existing = await _context.UserAccounts.FirstOrDefaultAsync(a => a.EmployeeId == emp.Id);
        if (existing != null)
        {
            TempData["Toast"] = "Bu çalışan için zaten bir hesap var.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            return View(new CreateEmployeeAccountVm
            {
                EmployeeId = emp.Id,
                EmployeeName = emp.FirstName + " " + emp.LastName,
                Email = emp.Email
            });
        }

        var account = new UserAccount
        {
            Email = emp.Email,
            Role = AccountController.Roles.Employee,
            EmployeeId = emp.Id,
        };
        account.PasswordHash = _passwordHasher.HashPassword(account, post.Password!);

        _context.UserAccounts.Add(account);
        await _context.SaveChangesAsync();

        TempData["Toast"] = "Çalışan hesabı oluşturuldu ve şifre atandı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> SetPassword(int id)
    {
        var account = await _context.UserAccounts.AsNoTracking().Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
        if (account is null) return NotFound();

        return View(new SetPasswordVm
        {
            AccountId = account.Id,
            Email = account.Email,
            Role = account.Role,
            EmployeeName = account.Employee == null ? null : (account.Employee.FirstName + " " + account.Employee.LastName)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPassword(SetPasswordPost post)
    {
        if (post.AccountId <= 0)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(post.NewPassword) || post.NewPassword.Length < 8)
        {
            ModelState.AddModelError(nameof(post.NewPassword), "Password must be at least 8 characters.");
        }

        if (post.NewPassword != post.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(post.ConfirmPassword), "Passwords do not match.");
        }

        var account = await _context.UserAccounts.FirstOrDefaultAsync(a => a.Id == post.AccountId);
        if (account is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(new SetPasswordVm
            {
                AccountId = account.Id,
                Email = account.Email,
                Role = account.Role,
                EmployeeName = null
            });
        }

        account.PasswordHash = _passwordHasher.HashPassword(account, post.NewPassword!);
        account.PasswordResetTokenHash = null;
        account.PasswordResetTokenExpiresAt = null;
        await _context.SaveChangesAsync();

        TempData["Toast"] = "Şifre güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    public sealed class AdminIndexVm
    {
        public IReadOnlyList<AccountRow> Accounts { get; init; } = Array.Empty<AccountRow>();
        public IReadOnlyList<EmployeeRow> EmployeesWithoutAccount { get; init; } = Array.Empty<EmployeeRow>();
    }

    public sealed class AccountRow
    {
        public int Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public int? EmployeeId { get; init; }
        public string? EmployeeName { get; init; }
    }

    public sealed class EmployeeRow
    {
        public int Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
    }

    public sealed class CreateHrVm
    {
        public string? Email { get; init; }
    }

    public sealed class CreateHrPost
    {
        public string? Email { get; init; }
        public string? Password { get; init; }
    }

    public sealed class CreateEmployeeAccountVm
    {
        public int EmployeeId { get; init; }
        public string EmployeeName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
    }

    public sealed class CreateEmployeeAccountPost
    {
        public int EmployeeId { get; init; }
        public string? Password { get; init; }
    }

    public sealed class SetPasswordVm
    {
        public int AccountId { get; init; }
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string? EmployeeName { get; init; }
    }

    public sealed class SetPasswordPost
    {
        public int AccountId { get; init; }
        public string? NewPassword { get; init; }
        public string? ConfirmPassword { get; init; }
    }
}
