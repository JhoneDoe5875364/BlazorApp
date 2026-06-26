using ClosedXML.Excel;
using HCP.HRPortal.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Data;

/// <summary>
/// Applies pending migrations and bootstraps the database on startup:
///   1. The four portal roles (Employee / Manager / HR / Finance) are ensured.
///   2. If the Employees table is empty AND MasterEmployee.xlsx exists next to the exe,
///      employees are loaded from that file (no mock data is seeded).
///   3. An Identity login is ensured for every employee in the DB, using the shared
///      default password — users change it later via /Account/ChangePassword.
///   4. PhotoUrl is back-filled to point at images/people/{slug}.jpg.
/// Safe to run on every boot — each step is idempotent.
/// </summary>
public static class DbInitializer
{
    private const string DefaultPassword = "Passw0rd!";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        try
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var env = sp.GetRequiredService<IWebHostEnvironment>();

            if (db.Database.IsRelational())
                await db.Database.MigrateAsync();
            else
                await db.Database.EnsureCreatedAsync();

            await SeedRolesAsync(sp);
            await SeedFromMasterXlsxAsync(db, env, logger);
            await SeedUsersAsync(db, sp);
            await BackfillPhotosAsync(db);
            await BackfillEmployeeNumbersAsync(db, logger);
            await SeedHolidaysAsync(db, logger);
            await SeedPoliciesAsync(db, logger);
            await SeedTimesheetProjectsAsync(db, logger);
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

    // ---------------------------------------------------------------- xlsx bootstrap
    /// <summary>
    /// Bootstraps the Employees table from MasterEmployee.xlsx placed next to the executable.
    /// Runs only when the Employees table is empty (so existing data is never overwritten).
    /// If the file is absent, the table simply stays empty — admins can still import later
    /// via the /employee-import page.
    /// </summary>
    private static async Task SeedFromMasterXlsxAsync(
        ApplicationDbContext db, IWebHostEnvironment env, ILogger logger)
    {
        if (await db.Employees.AnyAsync()) return;

        var xlsxPath = Path.Combine(env.ContentRootPath, "MasterEmployee.xlsx");
        if (!File.Exists(xlsxPath))
        {
            logger.LogWarning(
                "No MasterEmployee.xlsx found at {Path} — Employees table is empty. "
                + "Sign in cannot succeed until employees are imported via /employee-import.",
                xlsxPath);
            return;
        }

        using var workbook = new XLWorkbook(xlsxPath);
        var sheet = workbook.Worksheets
            .FirstOrDefault(s => s.Name.Equals("Employee Master", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheet(1);

        // Locate the header row (master file has a junk row 1).
        int headerRowIdx = 0;
        var colMap = new Dictionary<string, int>();
        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (int r = 1; r <= Math.Min(5, lastRow); r++)
        {
            var hdr = sheet.Row(r);
            var map = new Dictionary<string, int>();
            for (int c = 1; c <= 32; c++)
            {
                var v = Clean(hdr.Cell(c).GetString()).ToLowerInvariant();
                if (v.Length > 0) map[v] = c;
            }
            if (map.Keys.Any(k => k.Contains("email"))
                && map.Keys.Any(k => k.Contains("department"))
                && map.Keys.Any(k => k.Contains("name")))
            {
                headerRowIdx = r;
                colMap = map;
                break;
            }
        }

        if (headerRowIdx == 0)
        {
            logger.LogError("MasterEmployee.xlsx: no header row containing name+email+department was found.");
            return;
        }

        int Col(params string[] keywords)
        {
            foreach (var kw in keywords)
            {
                var hit = colMap.Keys.FirstOrDefault(k => k.Contains(kw));
                if (hit is not null) return colMap[hit];
            }
            return 0;
        }

        int cName = Col("full legal name", "full name", "name");
        int cEmail = Col("work email", "email");
        int cDept = Col("department");
        int cTitle = Col("job title", "title");
        int cMgr = Col("direct manager", "manager");
        int cLoc = Col("work location", "location");
        int cPhone = Col("mobile", "phone");
        int cHire = Col("start date", "hire date", "joined");
        int cStatus = Col("employment status", "status");
        int cPreferred = Col("preferred name", "preferred");
        int cPersonal = Col("personal email");
        int cBirthday = Col("birthday", "birth date", "dob");
        int cEmpType = Col("employment type", "type");
        int cProbation = Col("probation end", "probation");
        int cContract = Col("contract end");
        int cCountry = Col("country of employment", "country");

        var seenEmails = new HashSet<string>();
        int added = 0;
        for (int r = headerRowIdx + 1; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            string name = Clean(row.Cell(cName).GetString());
            if (name.Length == 0) continue;

            string email = Clean(row.Cell(cEmail).GetString()).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) continue;
            if (!seenEmails.Add(email)) continue;

            string dept = Clean(row.Cell(cDept).GetString());
            string title = Clean(row.Cell(cTitle).GetString());

            db.Employees.Add(new Employee
            {
                FullName = name,
                Email = email,
                Department = dept,
                JobTitle = title,
                ManagerName = cMgr > 0 ? Clean(row.Cell(cMgr).GetString()) : "",
                Location = cLoc > 0 ? Clean(row.Cell(cLoc).GetString()) : "",
                Phone = cPhone > 0 ? Clean(row.Cell(cPhone).GetString()) : "",
                Status = NormalizeStatus(cStatus > 0 ? Clean(row.Cell(cStatus).GetString()) : "active"),
                HireDate = cHire > 0
                    ? (TryParseDate(row.Cell(cHire)) ?? DateOnly.FromDateTime(DateTime.Today))
                    : DateOnly.FromDateTime(DateTime.Today),
                AnnualLeaveTotal = 25,

                PreferredName = cPreferred > 0 ? NullIfEmpty(Clean(row.Cell(cPreferred).GetString())) : null,
                PersonalEmail = cPersonal > 0 ? NullIfEmpty(Clean(row.Cell(cPersonal).GetString()).ToLowerInvariant()) : null,
                Birthday = cBirthday > 0 ? TryParseDate(row.Cell(cBirthday)) : null,
                EmploymentType = cEmpType > 0 ? NullIfEmpty(Clean(row.Cell(cEmpType).GetString())) : null,
                ProbationEndDate = cProbation > 0 ? TryParseDate(row.Cell(cProbation)) : null,
                ContractEndDate = cContract > 0 ? TryParseDate(row.Cell(cContract)) : null,
                Country = cCountry > 0 ? NullIfEmpty(Clean(row.Cell(cCountry).GetString())) : null,
            });
            added++;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Bootstrapped {Count} employees from MasterEmployee.xlsx", added);
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
                EmployeeId = employee.Id,
            };
            var result = await userManager.CreateAsync(user, DefaultPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(user, Roles.For(employee));
        }
    }

    // ---------------------------------------------------------------- photos
    private static async Task BackfillPhotosAsync(ApplicationDbContext db)
    {
        var toFix = await db.Employees.Where(e => e.PhotoUrl == null).ToListAsync();
        foreach (var e in toFix)
            e.PhotoUrl = $"images/people/{Slug(e.FullName)}.jpg";
        if (toFix.Count > 0)
            await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------- holidays
    /// <summary>
    /// Seeds UAE / US / UK public holidays for 2026 and 2027 if not already present.
    /// Country shown in parentheses in the title, e.g. "Independence Day (US)".
    /// Idempotent: skips events that already exist (same title + date).
    /// </summary>
    private static async Task SeedHolidaysAsync(ApplicationDbContext db, ILogger logger)
    {
        var existing = await db.CalendarEvents
            .Where(c => c.Type == CalendarEventType.Holiday)
            .Select(c => new { c.Title, c.Date })
            .ToListAsync();
        var existingKey = existing.Select(x => (x.Title, x.Date)).ToHashSet();

        var holidays = new List<(string Country, string Name, DateOnly Date)>
        {
            // ------ UAE (Federal public holidays) ------
            ("UAE", "New Year's Day",                       new(2026, 1, 1)),
            ("UAE", "Eid al-Fitr (Day 1)",                  new(2026, 3, 20)),
            ("UAE", "Eid al-Fitr (Day 2)",                  new(2026, 3, 21)),
            ("UAE", "Eid al-Fitr (Day 3)",                  new(2026, 3, 22)),
            ("UAE", "Arafat Day",                           new(2026, 5, 27)),
            ("UAE", "Eid al-Adha (Day 1)",                  new(2026, 5, 28)),
            ("UAE", "Eid al-Adha (Day 2)",                  new(2026, 5, 29)),
            ("UAE", "Islamic New Year",                     new(2026, 6, 17)),
            ("UAE", "Prophet Muhammad's Birthday",          new(2026, 8, 26)),
            ("UAE", "Commemoration Day",                    new(2026, 12, 1)),
            ("UAE", "National Day (Day 1)",                 new(2026, 12, 2)),
            ("UAE", "National Day (Day 2)",                 new(2026, 12, 3)),

            ("UAE", "New Year's Day",                       new(2027, 1, 1)),
            ("UAE", "Eid al-Fitr (Day 1)",                  new(2027, 3, 10)),
            ("UAE", "Eid al-Fitr (Day 2)",                  new(2027, 3, 11)),
            ("UAE", "Eid al-Fitr (Day 3)",                  new(2027, 3, 12)),
            ("UAE", "Arafat Day",                           new(2027, 5, 16)),
            ("UAE", "Eid al-Adha (Day 1)",                  new(2027, 5, 17)),
            ("UAE", "Eid al-Adha (Day 2)",                  new(2027, 5, 18)),
            ("UAE", "Eid al-Adha (Day 3)",                  new(2027, 5, 19)),
            ("UAE", "Islamic New Year",                     new(2027, 6, 7)),
            ("UAE", "Prophet Muhammad's Birthday",          new(2027, 8, 16)),
            ("UAE", "Commemoration Day",                    new(2027, 12, 1)),
            ("UAE", "National Day (Day 1)",                 new(2027, 12, 2)),
            ("UAE", "National Day (Day 2)",                 new(2027, 12, 3)),

            // ------ US (Federal holidays) ------
            ("US",  "New Year's Day",                       new(2026, 1, 1)),
            ("US",  "Martin Luther King Jr. Day",           new(2026, 1, 19)),
            ("US",  "Presidents' Day",                      new(2026, 2, 16)),
            ("US",  "Memorial Day",                         new(2026, 5, 25)),
            ("US",  "Juneteenth",                           new(2026, 6, 19)),
            ("US",  "Independence Day (observed)",          new(2026, 7, 3)),
            ("US",  "Labor Day",                            new(2026, 9, 7)),
            ("US",  "Columbus Day",                         new(2026, 10, 12)),
            ("US",  "Veterans Day",                         new(2026, 11, 11)),
            ("US",  "Thanksgiving Day",                     new(2026, 11, 26)),
            ("US",  "Christmas Day",                        new(2026, 12, 25)),

            ("US",  "New Year's Day",                       new(2027, 1, 1)),
            ("US",  "Martin Luther King Jr. Day",           new(2027, 1, 18)),
            ("US",  "Presidents' Day",                      new(2027, 2, 15)),
            ("US",  "Memorial Day",                         new(2027, 5, 31)),
            ("US",  "Juneteenth (observed)",                new(2027, 6, 18)),
            ("US",  "Independence Day (observed)",          new(2027, 7, 5)),
            ("US",  "Labor Day",                            new(2027, 9, 6)),
            ("US",  "Columbus Day",                         new(2027, 10, 11)),
            ("US",  "Veterans Day",                         new(2027, 11, 11)),
            ("US",  "Thanksgiving Day",                     new(2027, 11, 25)),
            ("US",  "Christmas Day (observed)",             new(2027, 12, 24)),

            // ------ UK (Bank holidays — England & Wales) ------
            ("UK",  "New Year's Day",                       new(2026, 1, 1)),
            ("UK",  "Good Friday",                          new(2026, 4, 3)),
            ("UK",  "Easter Monday",                        new(2026, 4, 6)),
            ("UK",  "Early May Bank Holiday",               new(2026, 5, 4)),
            ("UK",  "Spring Bank Holiday",                  new(2026, 5, 25)),
            ("UK",  "Summer Bank Holiday",                  new(2026, 8, 31)),
            ("UK",  "Christmas Day",                        new(2026, 12, 25)),
            ("UK",  "Boxing Day (observed)",                new(2026, 12, 28)),

            ("UK",  "New Year's Day",                       new(2027, 1, 1)),
            ("UK",  "Good Friday",                          new(2027, 3, 26)),
            ("UK",  "Easter Monday",                        new(2027, 3, 29)),
            ("UK",  "Early May Bank Holiday",               new(2027, 5, 3)),
            ("UK",  "Spring Bank Holiday",                  new(2027, 5, 31)),
            ("UK",  "Summer Bank Holiday",                  new(2027, 8, 30)),
            ("UK",  "Christmas Day (observed)",             new(2027, 12, 27)),
            ("UK",  "Boxing Day (observed)",                new(2027, 12, 28)),
        };

        int added = 0;
        foreach (var (country, name, date) in holidays)
        {
            var title = $"{name} ({country})";
            if (existingKey.Contains((title, date))) continue;
            db.CalendarEvents.Add(new CalendarEvent
            {
                Title = title,
                Date = date,
                Type = CalendarEventType.Holiday,
                Description = $"Public holiday in {country}.",
                Country = country,
                Scope = CalendarEventScope.Company,
                CreatedBy = "(system seed)",
            });
            added++;
        }
        if (added > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation(
                "Seeded {Count} initial public holidays (UAE/US/UK, 2026-2027). HR can edit / delete / add more from /holiday-admin.",
                added);
        }
    }

    // ---------------------------------------------------------------- policies
    /// <summary>
    /// Seeds the initial set of 9 company policies if the table is empty. HR can
    /// then add / edit / delete from the /policies page UI. We pre-populate the
    /// Diversity & Inclusion policy with its SharePoint URL since that one has
    /// been compiled and is intended for upload to Public HR Documents/Policies/.
    /// </summary>
    private static async Task SeedPoliciesAsync(ApplicationDbContext db, ILogger logger)
    {
        if (await db.CompanyPolicies.AnyAsync()) return;

        const string spRoot = "https://hansaconsultprojects815.sharepoint.com/sites/HCPHRHUB/Public%20HR%20Documents/Policies";

        var initial = new[]
        {
            new CompanyPolicy { Name = "Diversity & Inclusion Policy", Category = "HR", Version = "v1.0", Updated = "2025", Mandatory = true,
                DocumentUrl = $"{spRoot}/ST_HCP_Diversity_Inclusion_Policy.pdf" },
            new CompanyPolicy { Name = "Employee Handbook",            Category = "HR",         Version = "v2.0", Updated = "01 May 2026", Mandatory = true },
            new CompanyPolicy { Name = "Code of Conduct",              Category = "Compliance", Version = "v1.3", Updated = "25 Apr 2026", Mandatory = true },
            new CompanyPolicy { Name = "IT Security Policy",           Category = "IT",         Version = "v2.1", Updated = "01 May 2026", Mandatory = true },
            new CompanyPolicy { Name = "Travel & Expense Policy",      Category = "Finance",    Version = "v1.2", Updated = "12 May 2026", Mandatory = false },
            new CompanyPolicy { Name = "Data Privacy & GDPR",          Category = "Compliance", Version = "v3.0", Updated = "10 Apr 2026", Mandatory = true },
            new CompanyPolicy { Name = "Health & Safety",              Category = "HSE",        Version = "v1.5", Updated = "20 Mar 2026", Mandatory = true },
            new CompanyPolicy { Name = "Remote Work Policy",           Category = "HR",         Version = "v1.0", Updated = "15 Feb 2026", Mandatory = false },
            new CompanyPolicy { Name = "Anti-Bribery Policy",          Category = "Compliance", Version = "v2.2", Updated = "30 Jan 2026", Mandatory = true },
        };
        db.CompanyPolicies.AddRange(initial);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} initial company policies.", initial.Length);
    }

    // ---------------------------------------------------------------- timesheet projects
    /// <summary>Seeds the live project catalog (format: NUMBER-CLIENT-DESCRIPTION) on first boot,
    /// AND on subsequent boots cleans up the legacy "HCP-*" placeholder rows and adds any newly
    /// listed projects that aren't yet in the catalog. User-added rows are never touched.</summary>
    private static async Task SeedTimesheetProjectsAsync(ApplicationDbContext db, ILogger logger)
    {
        string[] raw =
        {
            "2401-NPMC-Anrria Design",
            "2402-JACB-PA Apron",
            "2403-WELK-South Africa Airport",
            "2405-JACB-KSIA Detailed Master Plan",
            "2406-RENA-Musandam Oman",
            "2407-POAC-",
            "2501-JACB-ABHA Storage Expansion",
            "2508-POAC-",
            "2512-VIT-",
            "2513-HHOP-Heathrow Operations",
            "2513.1-HHOP-",
            "2514-VITA-FSMPC Yap Phase 1",
            "2523-OILC-",
            "2524-OILC-",
            "2529-STO-Maldives Storage Tank",
            "2535-POAC-Vuda Tank Expansion",
            "2537-POAC-",
            "2541-VIT-",
            "2543-VIT-",
            "2548-ENOC-",
            "2554-POAC-",
            "2555-VIT-",
            "2556-NPT-",
            "2559-AMAN-Concourse A",
            "2563-PII-Hydrant Oversight",
            "2564-JACB-KSIA CR 32",
            "2565-FLCO-",
            "2567-JACB-KSIA CR 33 Cargo",
            "2569-AERT-ZIA Hydrant Expansion",
            "2577-QUAD-New Lisbon Airport",
            "2580-AERT-KSIA T3 & T4",
            "2581-POAC-Malau New Tank",
            "2583-JEDC-KSIA Consultancy",
            "2584-SACM-Montevideo Upgrade",
            "258-VIT-",
            "2586-QUAD-New Lisbon Airport BIM",
            "5000-POAC-",
            "5001-POAC-",
            "5002-POAC-",
            "5003-POAC-",
        };

        // Legacy cleanup: remove the older "HCP-ENG-*/HCP-PROJ-*/HCP-OPS-*/HCP-INT-*" placeholders
        // that the prototype used to seed. They are known defaults — safe to drop.
        var legacy = await db.TimesheetProjects
            .Where(p => p.Code.StartsWith("HCP-"))
            .ToListAsync();
        if (legacy.Count > 0)
        {
            db.TimesheetProjects.RemoveRange(legacy);
            await db.SaveChangesAsync();
            logger.LogInformation("Removed {Count} legacy HCP-* placeholder projects.", legacy.Count);
        }

        // Upsert the live catalog. Any user-added project (not matching a 'NUM-CLIENT' code on
        // the list) is left alone.
        var existing = await db.TimesheetProjects.Select(p => p.Code).ToListAsync();
        var added = 0;
        foreach (var line in raw)
        {
            var parts  = line.Split('-', 3);
            var num    = parts.Length > 0 ? parts[0] : line;
            var client = parts.Length > 1 ? parts[1] : "";
            var desc   = parts.Length > 2 ? parts[2] : "";
            var code   = $"{num}-{client}";
            if (existing.Contains(code)) continue;
            db.TimesheetProjects.Add(new TimesheetProject
            {
                Code     = code,
                Client   = string.IsNullOrEmpty(client) ? null : client,
                Name     = string.IsNullOrWhiteSpace(desc) ? "(no description)" : desc,
                IsActive = true
            });
            added++;
        }
        if (added > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Added {Count} new timesheet projects. Super Admin can edit at /project-admin.", added);
        }
    }

    // ---------------------------------------------------------------- employee number
    /// <summary>
    /// Assigns sequential EMP-### numbers to any employee that doesn't have one yet.
    /// Order: by Id (insertion order) so first-imported employees get the lowest numbers.
    /// Re-runs on every boot but is a no-op once every row has a number.
    /// </summary>
    private static async Task BackfillEmployeeNumbersAsync(ApplicationDbContext db, ILogger logger)
    {
        var existing = await db.Employees
            .Where(e => !string.IsNullOrEmpty(e.EmployeeNumber))
            .Select(e => e.EmployeeNumber)
            .ToListAsync();

        var maxNum = 0;
        foreach (var n in existing)
        {
            if (n.StartsWith("EMP-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(n[4..], out var v) && v > maxNum) maxNum = v;
        }

        var missing = await db.Employees
            .Where(e => string.IsNullOrEmpty(e.EmployeeNumber))
            .OrderBy(e => e.Id)
            .ToListAsync();

        if (missing.Count == 0) return;

        foreach (var e in missing)
        {
            maxNum++;
            e.EmployeeNumber = $"EMP-{maxNum:D3}";
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Assigned EMP-### numbers to {Count} employees (next free: EMP-{Next:D3}).",
            missing.Count, maxNum + 1);
    }

    /// <summary>Picks the next free EMP-### for a brand-new employee. Used by Add/Import.</summary>
    public static async Task<string> NextEmployeeNumberAsync(ApplicationDbContext db)
    {
        var max = await db.Employees
            .Where(e => e.EmployeeNumber.StartsWith("EMP-"))
            .Select(e => e.EmployeeNumber)
            .ToListAsync();
        var n = 0;
        foreach (var s in max)
            if (int.TryParse(s[4..], out var v) && v > n) n = v;
        return $"EMP-{(n + 1):D3}";
    }

    // ---------------------------------------------------------------- helpers
    private static string Slug(string name)
    {
        var s = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (s.Contains("--")) s = s.Replace("--", "-");
        return s.Trim('-');
    }

    private static string Clean(string s) =>
        string.IsNullOrWhiteSpace(s) ? "" : s.Replace(' ', ' ').Trim();

    private static string? NullIfEmpty(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    private static DateOnly? TryParseDate(IXLCell cell)
    {
        try
        {
            if (cell.DataType == XLDataType.DateTime)
                return DateOnly.FromDateTime(cell.GetDateTime());
        }
        catch { /* fall through to string parse */ }

        var s = Clean(cell.GetString());
        if (string.IsNullOrEmpty(s)) return null;

        string[] formats = { "MM/dd/yyyy", "M/d/yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" };
        if (DateOnly.TryParseExact(s, formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d)) return d;
        if (DateOnly.TryParse(s, out d)) return d;
        return null;
    }

    private static string NormalizeStatus(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "Active";
        if (s.Contains("terminat", StringComparison.OrdinalIgnoreCase)) return "Inactive";
        if (s.Contains("leave", StringComparison.OrdinalIgnoreCase)) return "On Leave";
        return "Active";
    }
}
