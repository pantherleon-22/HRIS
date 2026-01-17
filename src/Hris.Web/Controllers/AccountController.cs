using System.Security.Claims;
using Hris.Web.Data;
using Hris.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Hris.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly HrisDbContext _context;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;

    public AccountController(HrisDbContext context, IPasswordHasher<UserAccount> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        var vm = new LoginVm { ReturnUrl = returnUrl };

        return View(vm);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginPost post)
    {
        if (string.IsNullOrWhiteSpace(post.Email))
        {
            ModelState.AddModelError(nameof(post.Email), "Email is required.");
        }

        if (string.IsNullOrWhiteSpace(post.Password))
        {
            ModelState.AddModelError(nameof(post.Password), "Password is required.");
        }

        if (!ModelState.IsValid)
        {
            return View("Login", new LoginVm { ReturnUrl = post.ReturnUrl, Email = post.Email });
        }

        var normalizedEmail = (post.Email ?? "").Trim();
        var account = await _context.UserAccounts
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.Email == normalizedEmail);

        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View("Login", new LoginVm { ReturnUrl = post.ReturnUrl, Email = post.Email });
        }

        var verify = _passwordHasher.VerifyHashedPassword(account, account.PasswordHash, post.Password ?? "");
        if (verify == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View("Login", new LoginVm { ReturnUrl = post.ReturnUrl, Email = post.Email });
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Email),
            new(ClaimTypes.Role, account.Role),
            new(ClaimTypes.Email, account.Email),
        };

        if (account.Role == Roles.Employee && account.EmployeeId != null)
        {
            claims.Add(new(CustomClaims.EmployeeId, account.EmployeeId.Value.ToString()));
            if (account.Employee != null)
            {
                claims.Add(new(ClaimTypes.GivenName, account.Employee.FirstName));
                claims.Add(new(ClaimTypes.Surname, account.Employee.LastName));
            }
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToLocal(post.ReturnUrl, defaultEmployeeRedirect: true);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordVm());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordPost post)
    {
        if (string.IsNullOrWhiteSpace(post.Email))
        {
            ModelState.AddModelError(nameof(post.Email), "Email is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(new ForgotPasswordVm { Email = post.Email });
        }

        var email = post.Email!.Trim();
        var account = await _context.UserAccounts.FirstOrDefaultAsync(a => a.Email == email);

        // Demo-friendly: if account exists, we generate a token and show it on confirmation.
        if (account is null)
        {
            return View("ForgotPasswordConfirmation", new ForgotPasswordConfirmationVm { Email = email });
        }

        var token = GenerateToken();
        account.PasswordResetTokenHash = Sha256(token);
        account.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);
        await _context.SaveChangesAsync();

        return View("ForgotPasswordConfirmation", new ForgotPasswordConfirmationVm
        {
            Email = email,
            Token = token
        });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string email, string token)
    {
        return View(new ResetPasswordVm { Email = email, Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordPost post)
    {
        if (string.IsNullOrWhiteSpace(post.Email))
        {
            ModelState.AddModelError(nameof(post.Email), "Email is required.");
        }

        if (string.IsNullOrWhiteSpace(post.Token))
        {
            ModelState.AddModelError(nameof(post.Token), "Token is required.");
        }

        if (string.IsNullOrWhiteSpace(post.NewPassword) || post.NewPassword.Length < 8)
        {
            ModelState.AddModelError(nameof(post.NewPassword), "Password must be at least 8 characters.");
        }

        if (post.NewPassword != post.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(post.ConfirmPassword), "Passwords do not match.");
        }

        if (!ModelState.IsValid)
        {
            return View(new ResetPasswordVm { Email = post.Email ?? "", Token = post.Token ?? "" });
        }

        var email = post.Email!.Trim();
        var account = await _context.UserAccounts.FirstOrDefaultAsync(a => a.Email == email);
        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid token.");
            return View(new ResetPasswordVm { Email = post.Email ?? "", Token = post.Token ?? "" });
        }

        var tokenHash = Sha256(post.Token!);
        if (string.IsNullOrWhiteSpace(account.PasswordResetTokenHash) ||
            account.PasswordResetTokenExpiresAt == null ||
            account.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow ||
            !string.Equals(account.PasswordResetTokenHash, tokenHash, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Invalid or expired token.");
            return View(new ResetPasswordVm { Email = post.Email ?? "", Token = post.Token ?? "" });
        }

        account.PasswordHash = _passwordHasher.HashPassword(account, post.NewPassword!);
        account.PasswordResetTokenHash = null;
        account.PasswordResetTokenExpiresAt = null;
        await _context.SaveChangesAsync();

        TempData["Toast"] = "Password updated. You can sign in now.";
        return RedirectToAction(nameof(Login), new { returnUrl = post.ReturnUrl });
    }

    private IActionResult RedirectToLocal(string? returnUrl, bool defaultEmployeeRedirect = false)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (defaultEmployeeRedirect && User.IsInRole(Roles.Employee))
        {
            return RedirectToAction("Index", "EmployeePortal");
        }

        return RedirectToAction("Index", "Home");
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Hr = "HR";
        public const string Employee = "Employee";

        // Convenience for [Authorize(Roles = ...)]
        public const string HrOrAdmin = "HR,Admin";
    }

    public static class CustomClaims
    {
        public const string EmployeeId = "employee_id";
    }

    public sealed class LoginVm
    {
        public string? ReturnUrl { get; init; }
        public string? Email { get; init; }
    }

    public sealed class LoginPost
    {
        public string? Email { get; init; }
        public string? Password { get; init; }
        public string? ReturnUrl { get; init; }
    }

    public sealed class ForgotPasswordVm
    {
        public string? Email { get; init; }
    }

    public sealed class ForgotPasswordPost
    {
        public string? Email { get; init; }
    }

    public sealed class ForgotPasswordConfirmationVm
    {
        public string Email { get; init; } = string.Empty;
        public string? Token { get; init; }
    }

    public sealed class ResetPasswordVm
    {
        public string Email { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
    }

    public sealed class ResetPasswordPost
    {
        public string? Email { get; init; }
        public string? Token { get; init; }
        public string? NewPassword { get; init; }
        public string? ConfirmPassword { get; init; }
        public string? ReturnUrl { get; init; }
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string Sha256(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
