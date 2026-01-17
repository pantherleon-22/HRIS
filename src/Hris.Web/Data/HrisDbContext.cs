using Hris.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Hris.Web.Data;

public sealed class HrisDbContext : DbContext
{
    public HrisDbContext(DbContextOptions<HrisDbContext> options)
        : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Title> Titles => Set<Title>();
    public DbSet<JobType> JobTypes => Set<JobType>();
    public DbSet<JobTypeAllowedTitle> JobTypeAllowedTitles => Set<JobTypeAllowedTitle>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<DisciplinaryAction> DisciplinaryActions => Set<DisciplinaryAction>();

    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Availability> Availabilities => Set<Availability>();
    public DbSet<ShiftApplication> ShiftApplications => Set<ShiftApplication>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<EmployeeMessage> EmployeeMessages => Set<EmployeeMessage>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<JobTypeAllowedTitle>()
            .HasKey(x => new { x.JobTypeId, x.TitleId });

        modelBuilder.Entity<Employee>()
            .HasIndex(e => e.Email)
            .IsUnique();

        modelBuilder.Entity<Employee>()
            .Property(e => e.ExternalExperienceYears)
            .HasPrecision(4, 1);

        modelBuilder.Entity<Shift>()
            .HasIndex(s => new { s.JobTypeId, s.WorkDate, s.StartTime, s.EndTime });

        modelBuilder.Entity<Availability>()
            .HasIndex(a => new { a.EmployeeId, a.WorkDate });

        modelBuilder.Entity<ShiftAssignment>()
            .HasIndex(sa => new { sa.ShiftId, sa.EmployeeId })
            .IsUnique();

        modelBuilder.Entity<ShiftAssignment>()
            .HasOne(sa => sa.Shift)
            .WithMany()
            .HasForeignKey(sa => sa.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ShiftAssignment>()
            .HasOne(sa => sa.Employee)
            .WithMany()
            .HasForeignKey(sa => sa.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmployeeMessage>()
            .HasIndex(m => new { m.EmployeeId, m.CreatedAt });

        modelBuilder.Entity<EmployeeMessage>()
            .HasOne(m => m.Employee)
            .WithMany()
            .HasForeignKey(m => m.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserAccount>()
            .HasIndex(a => a.Email)
            .IsUnique();

        modelBuilder.Entity<UserAccount>()
            .HasIndex(a => new { a.Role, a.EmployeeId });

        modelBuilder.Entity<UserAccount>()
            .HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
