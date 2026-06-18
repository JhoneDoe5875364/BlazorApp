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
