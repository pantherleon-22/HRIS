using Hris.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hris.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(HrisDbContext db)
    {
        var hasher = new PasswordHasher<UserAccount>();

        var departmentNames = new[]
        {
            "İnsan Kaynakları",
            "Operasyon",
            "Bilgi Teknolojileri",
            "Finans",
            "Muhasebe",
            "Satın Alma",
            "Lojistik",
            "Depo",
            "Satış",
            "Pazarlama",
            "Müşteri Hizmetleri",
            "Kalite",
        };

        var existingDepartments = await db.Departments
            .Select(d => d.Name)
            .ToListAsync();

        var missingDepartments = departmentNames
            .Except(existingDepartments)
            .Select(name => new Department { Name = name });

        db.Departments.AddRange(missingDepartments);

        if (!await db.Titles.AnyAsync())
        {
            db.Titles.AddRange(
                new Title { Name = "Uzman" },
                new Title { Name = "Kıdemli Uzman" },
                new Title { Name = "Müdür" });
        }

        if (!await db.JobTypes.AnyAsync())
        {
            db.JobTypes.AddRange(
                new JobType { Name = "Genel" },
                new JobType { Name = "Kasiyer" },
                new JobType { Name = "Depo" });
        }

        await db.SaveChangesAsync();

        // Demo employees (for login testing)
        {
            var dept = await db.Departments.OrderBy(d => d.Id).FirstAsync();
            var title = await db.Titles.OrderBy(t => t.Id).FirstAsync();

            if (!await db.Employees.AnyAsync(e => e.Email == "employee1@hris.local"))
            {
                db.Employees.Add(new Employee
                {
                    FirstName = "Ahmet",
                    LastName = "Yılmaz",
                    Email = "employee1@hris.local",
                    Phone = "+90 555 000 0001",
                    HireDate = DateTime.UtcNow.Date.AddYears(-2),
                    ExternalExperienceYears = 1,
                    DepartmentId = dept.Id,
                    TitleId = title.Id,
                });
            }

            if (!await db.Employees.AnyAsync(e => e.Email == "employee2@hris.local"))
            {
                db.Employees.Add(new Employee
                {
                    FirstName = "Elif",
                    LastName = "Demir",
                    Email = "employee2@hris.local",
                    Phone = "+90 555 000 0002",
                    HireDate = DateTime.UtcNow.Date.AddYears(-1),
                    ExternalExperienceYears = 0,
                    DepartmentId = dept.Id,
                    TitleId = title.Id,
                });
            }

            await db.SaveChangesAsync();
        }

        // Demo accounts stored in SQL
        var adminEmail = "admin@hris.local";
        var adminPassword = "Admin123!";

        var existingAdmin = await db.UserAccounts.FirstOrDefaultAsync(a => a.Email == adminEmail);
        if (existingAdmin is null)
        {
            var admin = new UserAccount { Email = adminEmail, Role = Controllers.AccountController.Roles.Admin };
            admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
            db.UserAccounts.Add(admin);
            await db.SaveChangesAsync();
        }
        else if (!string.Equals(existingAdmin.Role, Controllers.AccountController.Roles.Admin, StringComparison.Ordinal))
        {
            // If the demo DB already had this user (previously as HR), promote to Admin.
            existingAdmin.Role = Controllers.AccountController.Roles.Admin;
            existingAdmin.PasswordHash = hasher.HashPassword(existingAdmin, adminPassword);
            await db.SaveChangesAsync();
        }

        var hrEmail = "hr@hris.local";
        var hrPassword = "Hr123!";

        var existingHr = await db.UserAccounts.FirstOrDefaultAsync(a => a.Email == hrEmail);
        if (existingHr is null)
        {
            var hr = new UserAccount { Email = hrEmail, Role = Controllers.AccountController.Roles.Hr };
            hr.PasswordHash = hasher.HashPassword(hr, hrPassword);
            db.UserAccounts.Add(hr);
            await db.SaveChangesAsync();
        }
        else if (!string.Equals(existingHr.Role, Controllers.AccountController.Roles.Hr, StringComparison.Ordinal))
        {
            existingHr.Role = Controllers.AccountController.Roles.Hr;
            existingHr.PasswordHash = hasher.HashPassword(existingHr, hrPassword);
            await db.SaveChangesAsync();
        }

        var demoEmployees = await db.Employees
            .AsNoTracking()
            .Where(e => e.Email == "employee1@hris.local" || e.Email == "employee2@hris.local")
            .ToListAsync();

        foreach (var emp in demoEmployees)
        {
            if (!await db.UserAccounts.AnyAsync(a => a.Email == emp.Email))
            {
                var acc = new UserAccount { Email = emp.Email, Role = Controllers.AccountController.Roles.Employee, EmployeeId = emp.Id };
                acc.PasswordHash = hasher.HashPassword(acc, "Employee123!");
                db.UserAccounts.Add(acc);
            }
        }

        await db.SaveChangesAsync();
    }
}
