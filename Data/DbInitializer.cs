using HCP.HRPortal.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Data;

/// <summary>
/// Applies pending migrations and seeds the database on startup:
/// the four roles, a login account per employee (role derived from department/title),
/// and the demo HR data. Safe to run repeatedly — it only seeds when tables are empty.
/// </summary>
public static class DbInitializer
{
    private const string DemoPassword = "Passw0rd!";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        try
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();

            // Relational (MySQL) → apply migrations. In-memory → just create the model.
            if (db.Database.IsRelational())
                await db.Database.MigrateAsync();
            else
                await db.Database.EnsureCreatedAsync();

            await SeedDomainDataAsync(db);
            await SeedInvoicesAsync(db);
            await SeedPerformanceAsync(db);
            await SeedGoalsAndFeedbackAsync(db);
            await SeedSalaryCertificatesAsync(db);
            await SeedTravelAsync(db);
            await SeedRolesAsync(sp);
            await SeedUsersAsync(db, sp);
            await BackfillPhotosAsync(db);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Database initialization failed. If using MySQL, ensure it is running and the " +
                "'DefaultConnection' connection string is correct; otherwise set " +
                "\"UseInMemoryDatabase\": true to run without a database.");
            throw;
        }
    }

    // ---------------------------------------------------------------- domain data
    private static async Task SeedDomainDataAsync(ApplicationDbContext db)
    {
        if (await db.Employees.AnyAsync())
            return;

        var today = DateOnly.FromDateTime(DateTime.Today);

        var employees = new List<Employee>
        {
            new() { FullName = "Sara Al-Mansouri", JobTitle = "Senior Software Engineer", Department = "Engineering", Email = "sara.almansouri@hcp.example", Phone = "+1 202 555 0142", Location = "Dubai HQ", HireDate = new(2019, 3, 11), ManagerName = "Daniel Okafor", AnnualSalary = 142000, AnnualLeaveTotal = 25, AnnualLeaveUsed = 8 },
            new() { FullName = "Daniel Okafor", JobTitle = "Engineering Manager", Department = "Engineering", Email = "daniel.okafor@hcp.example", Phone = "+1 202 555 0177", Location = "Dubai HQ", HireDate = new(2016, 7, 1), ManagerName = "Priya Nair", AnnualSalary = 188000, AnnualLeaveTotal = 28, AnnualLeaveUsed = 12 },
            new() { FullName = "Priya Nair", JobTitle = "VP of Engineering", Department = "Engineering", Email = "priya.nair@hcp.example", Phone = "+1 202 555 0190", Location = "London", HireDate = new(2014, 1, 20), ManagerName = "Executive Board", AnnualSalary = 245000, AnnualLeaveTotal = 30, AnnualLeaveUsed = 5 },
            new() { FullName = "Marcus Lindqvist", JobTitle = "Product Designer", Department = "Design", Email = "marcus.lindqvist@hcp.example", Phone = "+1 202 555 0118", Location = "London", HireDate = new(2021, 9, 6), ManagerName = "Hannah Weiss", AnnualSalary = 98000, AnnualLeaveTotal = 25, AnnualLeaveUsed = 15 },
            new() { FullName = "Hannah Weiss", JobTitle = "Head of Design", Department = "Design", Email = "hannah.weiss@hcp.example", Phone = "+1 202 555 0133", Location = "London", HireDate = new(2017, 5, 15), ManagerName = "Priya Nair", AnnualSalary = 165000, AnnualLeaveTotal = 28, AnnualLeaveUsed = 9, Status = "On Leave" },
            new() { FullName = "Yusuf Demir", JobTitle = "Financial Analyst", Department = "Finance", Email = "yusuf.demir@hcp.example", Phone = "+1 202 555 0155", Location = "Dubai HQ", HireDate = new(2020, 11, 2), ManagerName = "Elena Rossi", AnnualSalary = 92000, AnnualLeaveTotal = 25, AnnualLeaveUsed = 6 },
            new() { FullName = "Elena Rossi", JobTitle = "Finance Director", Department = "Finance", Email = "elena.rossi@hcp.example", Phone = "+1 202 555 0166", Location = "Dubai HQ", HireDate = new(2015, 2, 9), ManagerName = "Executive Board", AnnualSalary = 198000, AnnualLeaveTotal = 30, AnnualLeaveUsed = 18 },
            new() { FullName = "Grace Chen", JobTitle = "HR Business Partner", Department = "Human Resources", Email = "grace.chen@hcp.example", Phone = "+1 202 555 0144", Location = "Singapore", HireDate = new(2018, 8, 27), ManagerName = "Omar Haddad", AnnualSalary = 105000, AnnualLeaveTotal = 25, AnnualLeaveUsed = 11 },
            new() { FullName = "Omar Haddad", JobTitle = "Chief People Officer", Department = "Human Resources", Email = "omar.haddad@hcp.example", Phone = "+1 202 555 0100", Location = "Dubai HQ", HireDate = new(2013, 6, 3), ManagerName = "Executive Board", AnnualSalary = 232000, AnnualLeaveTotal = 30, AnnualLeaveUsed = 14 },
            new() { FullName = "Leila Haddad", JobTitle = "Talent Acquisition Lead", Department = "Human Resources", Email = "leila.haddad@hcp.example", Phone = "+1 202 555 0121", Location = "Singapore", HireDate = new(2022, 1, 17), ManagerName = "Omar Haddad", AnnualSalary = 88000, AnnualLeaveTotal = 25, AnnualLeaveUsed = 4 },
            new() { FullName = "Tom Becker", JobTitle = "Sales Account Executive", Department = "Sales", Email = "tom.becker@hcp.example", Phone = "+1 202 555 0188", Location = "New York", HireDate = new(2021, 4, 12), ManagerName = "Aisha Khan", AnnualSalary = 96000, AnnualLeaveTotal = 25, AnnualLeaveUsed = 20 },
            new() { FullName = "Aisha Khan", JobTitle = "Regional Sales Director", Department = "Sales", Email = "aisha.khan@hcp.example", Phone = "+1 202 555 0199", Location = "New York", HireDate = new(2017, 10, 30), ManagerName = "Executive Board", AnnualSalary = 175000, AnnualLeaveTotal = 28, AnnualLeaveUsed = 7 },
            // Engineering ICs reporting to Daniel Okafor (gives the Manager demo a real team).
            new() { FullName = "Kenji Tanaka", JobTitle = "Software Engineer", Department = "Engineering", Email = "kenji.tanaka@hcp.example", Phone = "+1 202 555 0211", Location = "Singapore", HireDate = new(2022, 3, 14), ManagerName = "Daniel Okafor", AnnualSalary = 118000, AnnualLeaveTotal = 25, AnnualLeaveUsed = 5 },
            new() { FullName = "Fatima Noor", JobTitle = "QA Engineer", Department = "Engineering", Email = "fatima.noor@hcp.example", Phone = "+1 202 555 0222", Location = "Dubai HQ", HireDate = new(2023, 1, 9), ManagerName = "Daniel Okafor", AnnualSalary = 102000, AnnualLeaveTotal = 25, AnnualLeaveUsed = 10, Status = "On Leave" },
            new() { FullName = "Diego Alvarez", JobTitle = "DevOps Engineer", Department = "Engineering", Email = "diego.alvarez@hcp.example", Phone = "+1 202 555 0233", Location = "London", HireDate = new(2020, 6, 22), ManagerName = "Daniel Okafor", AnnualSalary = 126000, AnnualLeaveTotal = 26, AnnualLeaveUsed = 13 },
        };
        db.Employees.AddRange(employees);
        await db.SaveChangesAsync();

        var byName = employees.ToDictionary(e => e.FullName, e => e.Id);

        db.TimeOffRequests.AddRange(
            new() { EmployeeId = byName["Sara Al-Mansouri"], EmployeeName = "Sara Al-Mansouri", Type = TimeOffType.Annual, StartDate = today.AddDays(14), EndDate = today.AddDays(20), Reason = "Family holiday", Status = TimeOffStatus.Approved, RequestedOn = today.AddDays(-10) },
            new() { EmployeeId = byName["Sara Al-Mansouri"], EmployeeName = "Sara Al-Mansouri", Type = TimeOffType.Sick, StartDate = today.AddDays(-5), EndDate = today.AddDays(-4), Reason = "Flu", Status = TimeOffStatus.Approved, RequestedOn = today.AddDays(-5) },
            new() { EmployeeId = byName["Marcus Lindqvist"], EmployeeName = "Marcus Lindqvist", Type = TimeOffType.Annual, StartDate = today.AddDays(30), EndDate = today.AddDays(37), Reason = "Vacation", Status = TimeOffStatus.Pending, RequestedOn = today.AddDays(-2) },
            new() { EmployeeId = byName["Tom Becker"], EmployeeName = "Tom Becker", Type = TimeOffType.Parental, StartDate = today.AddDays(45), EndDate = today.AddDays(75), Reason = "Parental leave", Status = TimeOffStatus.Pending, RequestedOn = today.AddDays(-1) },
            new() { EmployeeId = byName["Sara Al-Mansouri"], EmployeeName = "Sara Al-Mansouri", Type = TimeOffType.Unpaid, StartDate = today.AddDays(-40), EndDate = today.AddDays(-39), Reason = "Personal matter", Status = TimeOffStatus.Rejected, RequestedOn = today.AddDays(-45) },
            new() { EmployeeId = byName["Kenji Tanaka"], EmployeeName = "Kenji Tanaka", Type = TimeOffType.Annual, StartDate = today.AddDays(10), EndDate = today.AddDays(14), Reason = "Trip home", Status = TimeOffStatus.Pending, RequestedOn = today.AddDays(-1) },
            new() { EmployeeId = byName["Diego Alvarez"], EmployeeName = "Diego Alvarez", Type = TimeOffType.Sick, StartDate = today.AddDays(2), EndDate = today.AddDays(3), Reason = "Medical appointment", Status = TimeOffStatus.Pending, RequestedOn = today },
            new() { EmployeeId = byName["Fatima Noor"], EmployeeName = "Fatima Noor", Type = TimeOffType.Annual, StartDate = today.AddDays(-2), EndDate = today.AddDays(5), Reason = "Annual leave", Status = TimeOffStatus.Approved, RequestedOn = today.AddDays(-12) }
        );

        db.ExpenseClaims.AddRange(
            new() { Title = "Client dinner - Acme Corp", Category = "Meals", Amount = 184.50m, Date = today.AddDays(-12), Status = ExpenseStatus.Reimbursed, SubmittedBy = "Sara Al-Mansouri", Notes = "Dinner with prospective client." },
            new() { Title = "Flight to London (conference)", Category = "Travel", Amount = 920.00m, Date = today.AddDays(-8), Status = ExpenseStatus.Approved, SubmittedBy = "Sara Al-Mansouri" },
            new() { Title = "Hotel - 3 nights", Category = "Accommodation", Amount = 645.00m, Date = today.AddDays(-8), Status = ExpenseStatus.Submitted, SubmittedBy = "Sara Al-Mansouri" },
            new() { Title = "Taxi to airport", Category = "Travel", Amount = 38.00m, Date = today.AddDays(-3), Status = ExpenseStatus.Submitted, SubmittedBy = "Sara Al-Mansouri" },
            new() { Title = "Software license (Figma)", Category = "Software", Amount = 144.00m, Date = today.AddDays(-20), Status = ExpenseStatus.Approved, SubmittedBy = "Marcus Lindqvist" },
            new() { Title = "Team lunch", Category = "Meals", Amount = 210.75m, Date = today.AddDays(-1), Status = ExpenseStatus.Submitted, SubmittedBy = "Daniel Okafor" },
            new() { Title = "Conference ticket", Category = "Training", Amount = 499.00m, Date = today.AddDays(-30), Status = ExpenseStatus.Rejected, SubmittedBy = "Tom Becker", Notes = "Out of budget this quarter." }
        );

        db.Documents.AddRange(
            new() { Name = "Passport", Type = "Identity", Owner = "Sara Al-Mansouri", IssueDate = new(2018, 6, 1), ExpiryDate = today.AddDays(420) },
            new() { Name = "UAE Work Visa", Type = "Visa", Owner = "Sara Al-Mansouri", IssueDate = new(2022, 4, 1), ExpiryDate = today.AddDays(35) },
            new() { Name = "Employment Contract", Type = "Contract", Owner = "Sara Al-Mansouri", IssueDate = new(2019, 3, 11), ExpiryDate = today.AddDays(900) },
            new() { Name = "Health Insurance Card", Type = "Insurance", Owner = "Sara Al-Mansouri", IssueDate = new(2024, 1, 1), ExpiryDate = today.AddDays(120) },
            new() { Name = "Driving Licence", Type = "Identity", Owner = "Marcus Lindqvist", IssueDate = new(2019, 9, 9), ExpiryDate = today.AddDays(-15) },
            new() { Name = "Work Permit", Type = "Visa", Owner = "Tom Becker", IssueDate = new(2021, 4, 12), ExpiryDate = today.AddDays(50) },
            new() { Name = "Employment Contract", Type = "Contract", Owner = "Marcus Lindqvist", IssueDate = new(2021, 9, 6), ExpiryDate = today.AddDays(710) },
            new() { Name = "AWS Certification", Type = "Certificate", Owner = "Sara Al-Mansouri", IssueDate = new(2023, 5, 1), ExpiryDate = today.AddDays(-3) },
            new() { Name = "Health Insurance Card", Type = "Insurance", Owner = "Yusuf Demir", IssueDate = new(2024, 1, 1), ExpiryDate = today.AddDays(58) }
        );

        db.TrainingRecords.AddRange(
            new() { CourseName = "Security Awareness 2026", Category = "Compliance", EmployeeName = "Sara Al-Mansouri", Status = TrainingStatus.InProgress, ProgressPercent = 60, DueDate = today.AddDays(4), IsMandatory = true },
            new() { CourseName = "Code of Conduct", Category = "Compliance", EmployeeName = "Sara Al-Mansouri", Status = TrainingStatus.Completed, ProgressPercent = 100, DueDate = today.AddDays(-20), CompletedOn = today.AddDays(-25), IsMandatory = true },
            new() { CourseName = "Advanced C# & Blazor", Category = "Technical", EmployeeName = "Sara Al-Mansouri", Status = TrainingStatus.InProgress, ProgressPercent = 35, DueDate = today.AddDays(40), IsMandatory = false },
            new() { CourseName = "Leadership Essentials", Category = "Management", EmployeeName = "Daniel Okafor", Status = TrainingStatus.NotStarted, ProgressPercent = 0, DueDate = today.AddDays(30), IsMandatory = false },
            new() { CourseName = "Data Privacy & GDPR", Category = "Compliance", EmployeeName = "Sara Al-Mansouri", Status = TrainingStatus.NotStarted, ProgressPercent = 0, DueDate = today.AddDays(15), IsMandatory = true },
            new() { CourseName = "Design Systems Workshop", Category = "Technical", EmployeeName = "Marcus Lindqvist", Status = TrainingStatus.Completed, ProgressPercent = 100, DueDate = today.AddDays(-10), CompletedOn = today.AddDays(-12), IsMandatory = false },
            new() { CourseName = "Security Awareness 2026", Category = "Compliance", EmployeeName = "Kenji Tanaka", Status = TrainingStatus.Completed, ProgressPercent = 100, DueDate = today.AddDays(-2), CompletedOn = today.AddDays(-3), IsMandatory = true },
            new() { CourseName = "Security Awareness 2026", Category = "Compliance", EmployeeName = "Diego Alvarez", Status = TrainingStatus.InProgress, ProgressPercent = 45, DueDate = today.AddDays(7), IsMandatory = true },
            new() { CourseName = "Security Awareness 2026", Category = "Compliance", EmployeeName = "Fatima Noor", Status = TrainingStatus.NotStarted, ProgressPercent = 0, DueDate = today.AddDays(-1), IsMandatory = true }
        );

        db.CalendarEvents.AddRange(
            new() { Title = "Company All-Hands", Date = today.AddDays(2), Type = CalendarEventType.Meeting, Description = "Quarterly all-hands meeting in the main auditorium." },
            new() { Title = "National Day (Public Holiday)", Date = today.AddDays(6), Type = CalendarEventType.Holiday, Description = "Office closed." },
            new() { Title = "Security Awareness Training", Date = today.AddDays(4), Type = CalendarEventType.Training, Description = "Mandatory annual training - 1 hour." },
            new() { Title = "Sara on Annual Leave", Date = today.AddDays(14), Type = CalendarEventType.Leave, Description = "Out of office until the 20th." },
            new() { Title = "Q2 Expense Reports Due", Date = today.AddDays(9), Type = CalendarEventType.Deadline, Description = "Submit all outstanding expense claims." },
            new() { Title = "Daniel's Birthday", Date = today.AddDays(3), Type = CalendarEventType.Birthday, Description = "Wish Daniel a happy birthday!" },
            new() { Title = "Design Review", Date = today.AddDays(1), Type = CalendarEventType.Meeting, Description = "Review new portal design with stakeholders." },
            new() { Title = "Performance Review Cycle Opens", Date = today.AddDays(20), Type = CalendarEventType.Deadline, Description = "Self-assessments open in the portal." },
            // Finance-oriented reminders (surface in the Finance Due Dates calendar view).
            new() { Title = "Payroll cut-off", Date = today.AddDays(5), Type = CalendarEventType.Deadline, Description = "Submit all payroll changes before cut-off." },
            new() { Title = "Invoice INV-2045 due", Date = today.AddDays(8), Type = CalendarEventType.Deadline, Description = "Vendor invoice payment due." },
            new() { Title = "Timesheet submission deadline", Date = today.AddDays(3), Type = CalendarEventType.Deadline, Description = "All timesheets due for the period." },
            // Birthdays & anniversaries.
            new() { Title = "Kenji's Work Anniversary (4 yrs)", Date = today.AddDays(7), Type = CalendarEventType.Birthday, Description = "Celebrate Kenji Tanaka's 4-year milestone." }
        );

        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------- photos
    /// <summary>
    /// Points each employee without a photo at a headshot file under
    /// wwwroot/images/people/{slug}.jpg. Runs on every startup so existing databases
    /// get photos too; user-uploaded photos (non-null PhotoUrl) are left untouched, and
    /// a missing file harmlessly falls back to the initials avatar.
    /// </summary>
    private static async Task BackfillPhotosAsync(ApplicationDbContext db)
    {
        var toFix = await db.Employees.Where(e => e.PhotoUrl == null).ToListAsync();
        foreach (var e in toFix)
            e.PhotoUrl = $"images/people/{Slug(e.FullName)}.jpg";
        if (toFix.Count > 0)
            await db.SaveChangesAsync();
    }

    private static string Slug(string name)
    {
        var s = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (s.Contains("--")) s = s.Replace("--", "-");
        return s.Trim('-');
    }

    // ---------------------------------------------------------------- invoices
    /// <summary>Seeds sample vendor invoices if none exist (works for fresh and existing DBs).</summary>
    private static async Task SeedInvoicesAsync(ApplicationDbContext db)
    {
        if (await db.Invoices.AnyAsync()) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        db.Invoices.AddRange(
            new() { Number = "INV-1038", Vendor = "Cloud Co", Amount = 4200m, Currency = "USD", IssueDate = today.AddDays(-20), DueDate = today.AddDays(14), Status = InvoiceStatus.Unpaid },
            new() { Number = "INV-1039", Vendor = "OfficeSupplies Ltd", Amount = 890m, Currency = "USD", IssueDate = today.AddDays(-40), DueDate = today.AddDays(-1), Status = InvoiceStatus.Overdue },
            new() { Number = "INV-1040", Vendor = "TechRent Inc", Amount = 3150m, Currency = "USD", IssueDate = today.AddDays(-10), DueDate = today.AddDays(19), Status = InvoiceStatus.Unpaid },
            new() { Number = "INV-1041", Vendor = "Catering Pro", Amount = 620m, Currency = "USD", IssueDate = today.AddDays(-30), DueDate = today.AddDays(-7), Status = InvoiceStatus.Paid },
            new() { Number = "INV-1042", Vendor = "Swift Logistics", Amount = 1750m, Currency = "USD", IssueDate = today.AddDays(-35), DueDate = today.AddDays(-9), Status = InvoiceStatus.Paid },
            new() { Number = "INV-1043", Vendor = "PrintWorks", Amount = 380m, Currency = "USD", IssueDate = today.AddDays(-50), DueDate = today.AddDays(-17), Status = InvoiceStatus.Overdue },
            new() { Number = "INV-1044", Vendor = "Azure Infra Svc", Amount = 8900m, Currency = "USD", IssueDate = today.AddDays(-5), DueDate = today.AddDays(34), Status = InvoiceStatus.Unpaid },
            new() { Number = "INV-1045", Vendor = "Facilities Mgmt", Amount = 2100m, Currency = "USD", IssueDate = today.AddDays(-8), DueDate = today.AddDays(9), Status = InvoiceStatus.Unpaid }
        );
        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------- performance
    /// <summary>
    /// Seeds one performance review per employee for the current quarter. Ratings follow a
    /// fixed pattern so each team's band distribution (and the Manager donut) is stable across
    /// restarts; some reviews are pre-completed so "Reviews Due" reflects real outstanding work.
    /// </summary>
    private static async Task SeedPerformanceAsync(ApplicationDbContext db)
    {
        if (await db.PerformanceReviews.AnyAsync()) return;

        var employees = await db.Employees.ToListAsync();
        if (employees.Count == 0) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var quarter = (today.Month - 1) / 3 + 1;
        var quarterEndMonth = quarter * 3;
        var quarterEnd = new DateOnly(today.Year, quarterEndMonth, DateTime.DaysInMonth(today.Year, quarterEndMonth));
        var cycle = $"Q{quarter} {today.Year}";

        // Deterministic ratings → stable, realistic spread of bands per team.
        double[] ratingPattern = { 4.5, 3.8, 3.2, 4.2, 2.8, 3.6, 4.8, 3.0, 3.9, 4.1, 2.6, 3.4 };

        var reviews = new List<PerformanceReview>();
        for (var i = 0; i < employees.Count; i++)
        {
            var e = employees[i];
            var rating = ratingPattern[i % ratingPattern.Length];
            var band = rating >= 4.0 ? PerformanceBand.AboveExpectations
                     : rating >= 3.0 ? PerformanceBand.MeetsExpectations
                                     : PerformanceBand.BelowExpectations;
            reviews.Add(new PerformanceReview
            {
                EmployeeId = e.Id,
                EmployeeName = e.FullName,
                Cycle = cycle,
                Rating = rating,
                Band = band,
                DueDate = quarterEnd.AddDays(-(i % 10)),
                Completed = i % 4 == 0,
            });
        }

        db.PerformanceReviews.AddRange(reviews);
        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------- goals & feedback
    /// <summary>Seeds per-employee goals and peer/manager feedback for the current cycle.</summary>
    private static async Task SeedGoalsAndFeedbackAsync(ApplicationDbContext db)
    {
        if (await db.PerformanceGoals.AnyAsync()) return;

        var employees = await db.Employees.ToListAsync();
        if (employees.Count == 0) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var quarter = (today.Month - 1) / 3 + 1;
        var cycle = $"Q{quarter} {today.Year}";

        string[] goalPool =
        {
            "Deliver key project milestone",
            "Complete professional certification",
            "Mentor a junior colleague",
            "Improve a core process",
            "Lead a cross-team initiative",
            "Reduce operational backlog below target",
            "Document the team knowledge base",
            "Onboard a new team member",
        };
        int[] progressPool = { 90, 60, 75, 40, 20, 100, 50, 30 };

        string[] managerComments =
        {
            "Consistently delivers high-quality work and shows strong ownership this quarter.",
            "Great technical leadership — real, measurable impact on the team's delivery.",
            "Reliable and thorough; I'd like to see more proactive flagging of blockers.",
            "Strong quarter overall. Keep pushing on the stretch goals.",
        };
        string[] peerComments =
        {
            "Excellent collaborator — always willing to pair and share knowledge.",
            "Super helpful during a tricky release; really appreciated the support.",
            "Brings a calm, structured approach to problem solving.",
            "Great teammate who communicates clearly and follows through.",
        };

        var goals = new List<PerformanceGoal>();
        var feedback = new List<PerformanceFeedback>();

        for (var i = 0; i < employees.Count; i++)
        {
            var e = employees[i];

            for (var k = 0; k < 4; k++)
            {
                var progress = progressPool[(i + k) % progressPool.Length];
                var status = progress >= 100 ? GoalStatus.Completed
                           : progress >= 70 ? GoalStatus.OnTrack
                           : progress >= 40 ? GoalStatus.AtRisk
                                            : GoalStatus.Behind;
                goals.Add(new PerformanceGoal
                {
                    EmployeeId = e.Id,
                    EmployeeName = e.FullName,
                    Cycle = cycle,
                    Title = goalPool[(i + k) % goalPool.Length],
                    ProgressPercent = progress,
                    Status = status,
                });
            }

            var peer1 = employees[(i + 1) % employees.Count];
            var peer2 = employees[(i + 3) % employees.Count];
            feedback.Add(new PerformanceFeedback { EmployeeId = e.Id, EmployeeName = e.FullName, Author = e.ManagerName, Relation = "Manager", Comment = managerComments[i % managerComments.Length], Date = today.AddDays(-(7 + i % 6)) });
            feedback.Add(new PerformanceFeedback { EmployeeId = e.Id, EmployeeName = e.FullName, Author = peer1.FullName, Relation = "Peer", Comment = peerComments[i % peerComments.Length], Date = today.AddDays(-(14 + i % 8)) });
            feedback.Add(new PerformanceFeedback { EmployeeId = e.Id, EmployeeName = e.FullName, Author = peer2.FullName, Relation = "Peer", Comment = peerComments[(i + 1) % peerComments.Length], Date = today.AddDays(-(21 + i % 5)) });
        }

        db.PerformanceGoals.AddRange(goals);
        db.PerformanceFeedback.AddRange(feedback);
        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------- salary certificates
    /// <summary>Seeds a few salary-certificate requests (two left Pending/Processing).</summary>
    private static async Task SeedSalaryCertificatesAsync(ApplicationDbContext db)
    {
        if (await db.SalaryCertificateRequests.AnyAsync()) return;

        var employees = await db.Employees.ToListAsync();
        if (employees.Count == 0) return;
        int IdOf(string name) => employees.FirstOrDefault(e => e.FullName == name)?.Id ?? 0;

        var today = DateOnly.FromDateTime(DateTime.Today);
        db.SalaryCertificateRequests.AddRange(
            new() { EmployeeId = IdOf("Sara Al-Mansouri"), EmployeeName = "Sara Al-Mansouri", Purpose = "Bank loan", Addressee = "Emirates NBD", RequestedOn = today.AddDays(-3), Status = CertificateStatus.Pending },
            new() { EmployeeId = IdOf("Yusuf Demir"), EmployeeName = "Yusuf Demir", Purpose = "Visa application", Addressee = "UK Visas & Immigration", RequestedOn = today.AddDays(-5), Status = CertificateStatus.Processing },
            new() { EmployeeId = IdOf("Marcus Lindqvist"), EmployeeName = "Marcus Lindqvist", Purpose = "Rental agreement", Addressee = "Knight Frank Lettings", RequestedOn = today.AddDays(-12), Status = CertificateStatus.Issued },
            new() { EmployeeId = IdOf("Tom Becker"), EmployeeName = "Tom Becker", Purpose = "Bank loan", Addressee = "Chase Bank", RequestedOn = today.AddDays(-20), Status = CertificateStatus.Issued }
        );
        await db.SaveChangesAsync();
    }

    // --------------------------------------------------------------- travel
    /// <summary>Seeds business trips across employees (upcoming + past) for the Travel page and Manager KPI.</summary>
    private static async Task SeedTravelAsync(ApplicationDbContext db)
    {
        if (await db.BusinessTrips.AnyAsync()) return;

        var employees = await db.Employees.ToListAsync();
        if (employees.Count == 0) return;
        int IdOf(string name) => employees.FirstOrDefault(e => e.FullName == name)?.Id ?? 0;

        var today = DateOnly.FromDateTime(DateTime.Today);
        db.BusinessTrips.AddRange(
            // Daniel Okafor's direct reports → upcoming (drives Manager "Upcoming Travel").
            new() { EmployeeId = IdOf("Kenji Tanaka"), EmployeeName = "Kenji Tanaka", Destination = "Singapore", Purpose = "APAC engineering sync", DepartDate = today.AddDays(10), ReturnDate = today.AddDays(13), EstimatedCost = 2400m, Status = TripStatus.Approved },
            new() { EmployeeId = IdOf("Diego Alvarez"), EmployeeName = "Diego Alvarez", Destination = "London, UK", Purpose = "Infrastructure summit", DepartDate = today.AddDays(18), ReturnDate = today.AddDays(21), EstimatedCost = 2850m, Status = TripStatus.Pending },
            new() { EmployeeId = IdOf("Sara Al-Mansouri"), EmployeeName = "Sara Al-Mansouri", Destination = "New York, USA", Purpose = "Client QBR & summit", DepartDate = today.AddDays(8), ReturnDate = today.AddDays(12), EstimatedCost = 3200m, Status = TripStatus.Approved },
            new() { EmployeeId = IdOf("Fatima Noor"), EmployeeName = "Fatima Noor", Destination = "Berlin, Germany", Purpose = "QA automation conference", DepartDate = today.AddDays(25), ReturnDate = today.AddDays(28), EstimatedCost = 2600m, Status = TripStatus.Pending },
            // Sara — past trips so her personal Travel page is populated.
            new() { EmployeeId = IdOf("Sara Al-Mansouri"), EmployeeName = "Sara Al-Mansouri", Destination = "London, UK", Purpose = "Engineering offsite", DepartDate = today.AddDays(-30), ReturnDate = today.AddDays(-27), EstimatedCost = 2850m, Status = TripStatus.Completed },
            new() { EmployeeId = IdOf("Sara Al-Mansouri"), EmployeeName = "Sara Al-Mansouri", Destination = "Toronto, Canada", Purpose = "Partner onboarding workshop", DepartDate = today.AddDays(-60), ReturnDate = today.AddDays(-57), EstimatedCost = 3450m, Status = TripStatus.Completed },
            // Yusuf (Finance demo) — upcoming + past.
            new() { EmployeeId = IdOf("Yusuf Demir"), EmployeeName = "Yusuf Demir", Destination = "London, UK", Purpose = "Finance systems review", DepartDate = today.AddDays(15), ReturnDate = today.AddDays(17), EstimatedCost = 2100m, Status = TripStatus.Approved },
            new() { EmployeeId = IdOf("Yusuf Demir"), EmployeeName = "Yusuf Demir", Destination = "Singapore", Purpose = "APAC finance close", DepartDate = today.AddDays(-20), ReturnDate = today.AddDays(-16), EstimatedCost = 4100m, Status = TripStatus.Completed },
            new() { EmployeeId = IdOf("Tom Becker"), EmployeeName = "Tom Becker", Destination = "New York, USA", Purpose = "Sales summit", DepartDate = today.AddDays(5), ReturnDate = today.AddDays(8), EstimatedCost = 3200m, Status = TripStatus.Approved },
            new() { EmployeeId = IdOf("Marcus Lindqvist"), EmployeeName = "Marcus Lindqvist", Destination = "London, UK", Purpose = "Design leadership offsite", DepartDate = today.AddDays(22), ReturnDate = today.AddDays(25), EstimatedCost = 2850m, Status = TripStatus.Pending },
            new() { EmployeeId = IdOf("Aisha Khan"), EmployeeName = "Aisha Khan", Destination = "Sydney, Australia", Purpose = "Regional sales kick-off", DepartDate = today.AddDays(-80), ReturnDate = today.AddDays(-76), EstimatedCost = 5100m, Status = TripStatus.Rejected }
        );
        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------- roles
    private static async Task SeedRolesAsync(IServiceProvider sp)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // ---------------------------------------------------------------- users
    private static async Task SeedUsersAsync(ApplicationDbContext db, IServiceProvider sp)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var employee in await db.Employees.ToListAsync())
        {
            if (await userManager.FindByEmailAsync(employee.Email) is not null)
                continue;

            var user = new ApplicationUser
            {
                UserName = employee.Email,
                Email = employee.Email,
                EmailConfirmed = true,
                EmployeeId = employee.Id
            };

            var result = await userManager.CreateAsync(user, DemoPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(user, RoleFor(employee));
        }
    }

    /// <summary>Derives the portal role from an employee's department and job title.</summary>
    private static string RoleFor(Employee e)
    {
        if (e.Department.Equals("Human Resources", StringComparison.OrdinalIgnoreCase))
            return Roles.HR;
        if (e.Department.Equals("Finance", StringComparison.OrdinalIgnoreCase))
            return Roles.Finance;

        string[] leadership = { "Manager", "Director", "Head", "VP", "Chief", "Lead" };
        if (leadership.Any(k => e.JobTitle.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return Roles.Manager;

        return Roles.Employee;
    }
}
