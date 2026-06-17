using HCP.HRPortal.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Data;

/// <summary>
/// Single EF Core context for the portal. Inherits all the ASP.NET Core Identity
/// tables (AspNetUsers, AspNetRoles, ...) and adds the HR domain tables on top.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<TimeOffRequest> TimeOffRequests => Set<TimeOffRequest>();
    public DbSet<ExpenseClaim> ExpenseClaims => Set<ExpenseClaim>();
    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();
    public DbSet<TrainingRecord> TrainingRecords => Set<TrainingRecord>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();
    public DbSet<PerformanceGoal> PerformanceGoals => Set<PerformanceGoal>();
    public DbSet<PerformanceFeedback> PerformanceFeedback => Set<PerformanceFeedback>();
    public DbSet<SalaryCertificateRequest> SalaryCertificateRequests => Set<SalaryCertificateRequest>();
    public DbSet<BusinessTrip> BusinessTrips => Set<BusinessTrip>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // --- Employee -------------------------------------------------------
        b.Entity<Employee>(e =>
        {
            e.ToTable("Employees");
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            e.Property(x => x.JobTitle).HasMaxLength(120);
            e.Property(x => x.Department).HasMaxLength(80);
            e.Property(x => x.Email).HasMaxLength(160).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Location).HasMaxLength(80);
            e.Property(x => x.ManagerName).HasMaxLength(120);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.AnnualSalary).HasPrecision(12, 2);
            e.Property(x => x.PhotoUrl).HasMaxLength(256);
            e.HasIndex(x => x.Email).IsUnique();
        });

        // --- Identity user <-> Employee (1:1) -------------------------------
        b.Entity<ApplicationUser>(u =>
        {
            u.HasOne(x => x.Employee)
             .WithMany()
             .HasForeignKey(x => x.EmployeeId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // --- Time off -------------------------------------------------------
        b.Entity<TimeOffRequest>(e =>
        {
            e.ToTable("TimeOffRequests");
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.EmployeeName).HasMaxLength(120);
            e.Property(x => x.Reason).HasMaxLength(400);
            e.Property(x => x.AttachmentUrl).HasMaxLength(512);
            e.Property(x => x.AttachmentName).HasMaxLength(200);
            e.HasOne<Employee>()
             .WithMany()
             .HasForeignKey(x => x.EmployeeId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.Status);
        });

        // --- Expenses -------------------------------------------------------
        b.Entity<ExpenseClaim>(e =>
        {
            e.ToTable("ExpenseClaims");
            e.Property(x => x.Title).HasMaxLength(160);
            e.Property(x => x.Category).HasMaxLength(40);
            e.Property(x => x.Currency).HasMaxLength(8);
            e.Property(x => x.SubmittedBy).HasMaxLength(120);
            e.Property(x => x.Notes).HasMaxLength(400);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
        });

        // --- Documents ------------------------------------------------------
        b.Entity<DocumentRecord>(e =>
        {
            e.ToTable("Documents");
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Type).HasMaxLength(40);
            e.Property(x => x.Owner).HasMaxLength(120);
            e.HasIndex(x => x.ExpiryDate);
        });

        // --- Training -------------------------------------------------------
        b.Entity<TrainingRecord>(e =>
        {
            e.ToTable("TrainingRecords");
            e.Property(x => x.CourseName).HasMaxLength(160);
            e.Property(x => x.Category).HasMaxLength(40);
            e.Property(x => x.EmployeeName).HasMaxLength(120);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        // --- Invoices -------------------------------------------------------
        b.Entity<Invoice>(e =>
        {
            e.ToTable("Invoices");
            e.Property(x => x.Number).HasMaxLength(40).IsRequired();
            e.Property(x => x.Vendor).HasMaxLength(120);
            e.Property(x => x.Currency).HasMaxLength(8);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DueDate);
        });

        // --- Performance reviews --------------------------------------------
        b.Entity<PerformanceReview>(e =>
        {
            e.ToTable("PerformanceReviews");
            e.Property(x => x.EmployeeName).HasMaxLength(120);
            e.Property(x => x.Cycle).HasMaxLength(20);
            e.Property(x => x.Rating).HasPrecision(3, 2);
            e.Property(x => x.Band).HasConversion<string>().HasMaxLength(24);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.DueDate);
        });

        // --- Performance goals ----------------------------------------------
        b.Entity<PerformanceGoal>(e =>
        {
            e.ToTable("PerformanceGoals");
            e.Property(x => x.EmployeeName).HasMaxLength(120);
            e.Property(x => x.Cycle).HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.EmployeeId);
        });

        // --- Performance feedback -------------------------------------------
        b.Entity<PerformanceFeedback>(e =>
        {
            e.ToTable("PerformanceFeedback");
            e.Property(x => x.EmployeeName).HasMaxLength(120);
            e.Property(x => x.Author).HasMaxLength(120);
            e.Property(x => x.Relation).HasMaxLength(40);
            e.Property(x => x.Comment).HasMaxLength(600);
            e.HasIndex(x => x.EmployeeId);
        });

        // --- Salary certificate requests ------------------------------------
        b.Entity<SalaryCertificateRequest>(e =>
        {
            e.ToTable("SalaryCertificateRequests");
            e.Property(x => x.EmployeeName).HasMaxLength(120);
            e.Property(x => x.Purpose).HasMaxLength(120);
            e.Property(x => x.Addressee).HasMaxLength(160);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.Status);
        });

        // --- Business trips -------------------------------------------------
        b.Entity<BusinessTrip>(e =>
        {
            e.ToTable("BusinessTrips");
            e.Property(x => x.EmployeeName).HasMaxLength(120);
            e.Property(x => x.Destination).HasMaxLength(120);
            e.Property(x => x.Purpose).HasMaxLength(160);
            e.Property(x => x.EstimatedCost).HasPrecision(12, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.DepartDate);
        });

        // --- Calendar -------------------------------------------------------
        b.Entity<CalendarEvent>(e =>
        {
            e.ToTable("CalendarEvents");
            e.Property(x => x.Title).HasMaxLength(160);
            e.Property(x => x.Description).HasMaxLength(400);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Date);
        });
    }
}
