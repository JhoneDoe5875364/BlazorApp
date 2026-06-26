using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

public enum TimesheetStatus { Draft, Submitted, Approved }

public class TimesheetEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public string Project { get; set; } = "";
    public string Task { get; set; } = "";
    public string Description { get; set; } = "";
    public double Hours { get; set; }
}

public class TimesheetWeek
{
    public List<TimesheetEntry> Entries { get; set; } = new();
    public TimesheetStatus Status { get; set; } = TimesheetStatus.Draft;
}

/// <summary>
/// Per-user, per-week timesheets. In-memory singleton (prototype): edits and the submitted
/// status persist for the app lifetime. A new week is seeded with sample rows on first access.
/// Supports multiple projects per day with hours allocation against a standard 8-hour day.
/// </summary>
public interface ITimesheetService
{
    /// <summary>Standard length of a working day in hours (used for the % allocation display).</summary>
    double StandardDayHours { get; }

    /// <summary>Catalog of active projects employees can log time against.</summary>
    IReadOnlyList<TimesheetProject> Projects { get; }

    /// <summary>All projects including inactive ones — Super Admin view.</summary>
    IReadOnlyList<TimesheetProject> AllProjects { get; }

    void AddProject(TimesheetProject project);
    void UpdateProject(TimesheetProject project);
    void DeleteProject(int id);

    TimesheetWeek Get(string userEmail, DateOnly weekStart);
    void Submit(string userEmail, DateOnly weekStart);

    /// <summary>Append a new entry to the given week (must be Draft).</summary>
    void AddEntry(string userEmail, DateOnly weekStart, TimesheetEntry entry);

    /// <summary>Remove an entry by its Id from the given week (must be Draft).</summary>
    void RemoveEntry(string userEmail, DateOnly weekStart, Guid entryId);

    /// <summary>Number of submitted timesheet weeks still awaiting approval (Finance KPI).</summary>
    int PendingApprovalCount();
}

public class TimesheetService : ITimesheetService
{
    private readonly object _lock = new();
    private readonly Dictionary<(string Email, DateOnly Week), TimesheetWeek> _weeks = new();
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public double StandardDayHours => 8.0;

    public IReadOnlyList<TimesheetProject> Projects
    {
        get
        {
            using var db = _factory.CreateDbContext();
            return db.TimesheetProjects.Where(p => p.IsActive).OrderBy(p => p.Code).ToList();
        }
    }

    public IReadOnlyList<TimesheetProject> AllProjects
    {
        get
        {
            using var db = _factory.CreateDbContext();
            return db.TimesheetProjects.OrderBy(p => p.Code).ToList();
        }
    }

    public void AddProject(TimesheetProject project)
    {
        using var db = _factory.CreateDbContext();
        project.Id = 0;
        db.TimesheetProjects.Add(project);
        db.SaveChanges();
    }

    public void UpdateProject(TimesheetProject project)
    {
        using var db = _factory.CreateDbContext();
        var existing = db.TimesheetProjects.FirstOrDefault(p => p.Id == project.Id);
        if (existing is null) return;
        existing.Code = project.Code;
        existing.Name = project.Name;
        existing.IsActive = project.IsActive;
        existing.Client = project.Client;
        db.SaveChanges();
    }

    public void DeleteProject(int id)
    {
        using var db = _factory.CreateDbContext();
        var existing = db.TimesheetProjects.FirstOrDefault(p => p.Id == id);
        if (existing is null) return;
        db.TimesheetProjects.Remove(existing);
        db.SaveChanges();
    }

    public TimesheetService(IDbContextFactory<ApplicationDbContext> factory)
    {
        _factory = factory;
        // Pre-submit a few weeks so the Finance "Pending Timesheets" KPI reflects real stored
        // state on a fresh boot. The count rises as employees submit via the Timesheets page.
        var monday = WeekStart(DateOnly.FromDateTime(DateTime.Today));
        string[] submitters =
        {
            "kenji.tanaka@hcp.example",
            "diego.alvarez@hcp.example",
            "marcus.lindqvist@hcp.example",
            "tom.becker@hcp.example",
        };
        foreach (var email in submitters)
            _weeks[(email, monday)] = new TimesheetWeek { Entries = Seed(monday), Status = TimesheetStatus.Submitted };
    }

    public int PendingApprovalCount()
    {
        lock (_lock)
            return _weeks.Values.Count(w => w.Status == TimesheetStatus.Submitted);
    }

    public TimesheetWeek Get(string userEmail, DateOnly weekStart)
    {
        lock (_lock)
        {
            if (!_weeks.TryGetValue((userEmail, weekStart), out var w))
            {
                w = new TimesheetWeek { Entries = Seed(weekStart) };
                _weeks[(userEmail, weekStart)] = w;
            }
            return w;
        }
    }

    public void Submit(string userEmail, DateOnly weekStart)
    {
        lock (_lock)
        {
            if (!_weeks.TryGetValue((userEmail, weekStart), out var w))
            {
                w = new TimesheetWeek { Entries = Seed(weekStart) };
                _weeks[(userEmail, weekStart)] = w;
            }
            w.Status = TimesheetStatus.Submitted;
        }
    }

    public void AddEntry(string userEmail, DateOnly weekStart, TimesheetEntry entry)
    {
        lock (_lock)
        {
            if (!_weeks.TryGetValue((userEmail, weekStart), out var w))
            {
                w = new TimesheetWeek { Entries = Seed(weekStart) };
                _weeks[(userEmail, weekStart)] = w;
            }
            if (w.Status == TimesheetStatus.Draft)
                w.Entries.Add(entry);
        }
    }

    public void RemoveEntry(string userEmail, DateOnly weekStart, Guid entryId)
    {
        lock (_lock)
        {
            if (_weeks.TryGetValue((userEmail, weekStart), out var w) && w.Status == TimesheetStatus.Draft)
                w.Entries.RemoveAll(e => e.Id == entryId);
        }
    }

    /// <summary>Monday of the week containing <paramref name="d"/>.</summary>
    private static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    // Seed: demonstrates both single-project days (Mon/Fri) and multi-project days
    // split by % of the 8-hour standard (Tue/Wed/Thu).
    private static List<TimesheetEntry> Seed(DateOnly mon) => new()
    {
        new() { Date = mon,            Project = "HCP-ENG-001",  Task = "Site inspection",   Description = "Inspection and report",   Hours = 8.0 },
        new() { Date = mon.AddDays(1), Project = "HCP-PROJ-002", Task = "Project planning",  Description = "Sprint planning session", Hours = 5.0 },
        new() { Date = mon.AddDays(1), Project = "HCP-INT-001",  Task = "Internal training", Description = "Security awareness",      Hours = 3.0 },
        new() { Date = mon.AddDays(2), Project = "HCP-ENG-002",  Task = "Mechanical design", Description = "Design review",           Hours = 6.0 },
        new() { Date = mon.AddDays(2), Project = "HCP-PROJ-004", Task = "Documentation",     Description = "Design notes write-up",   Hours = 2.0 },
        new() { Date = mon.AddDays(3), Project = "HCP-PROJ-003", Task = "Client meeting",    Description = "Requirements gathering",  Hours = 4.0 },
        new() { Date = mon.AddDays(3), Project = "HCP-PROJ-004", Task = "Meeting notes",     Description = "Follow-up actions",       Hours = 2.0 },
        new() { Date = mon.AddDays(4), Project = "HCP-ENG-003",  Task = "Drawing review",    Description = "Final drawings",          Hours = 8.0 },
    };
}
