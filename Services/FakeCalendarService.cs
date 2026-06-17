using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

/// <summary>Calendar events, backed by MySQL via EF Core.</summary>
public class FakeCalendarService : ICalendarService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public FakeCalendarService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public IEnumerable<CalendarEvent> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.CalendarEvents.OrderBy(e => e.Date).ToList();
    }

    public IEnumerable<CalendarEvent> GetUpcoming(int count = 5)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        using var db = _factory.CreateDbContext();
        return db.CalendarEvents.Where(e => e.Date >= today)
                 .OrderBy(e => e.Date).Take(count).ToList();
    }

    public IEnumerable<CalendarEvent> GetForMonth(int year, int month)
    {
        using var db = _factory.CreateDbContext();
        return db.CalendarEvents.Where(e => e.Date.Year == year && e.Date.Month == month)
                 .OrderBy(e => e.Date).ToList();
    }

    // ---------------------------------------------------------------------------
    // Role-aware aggregated calendar (brief §7). Pulls items from several sources
    // (company events, leave, document expiries, training) and filters by view.
    // ---------------------------------------------------------------------------
    public IReadOnlyList<CalendarItem> GetItems(CalendarView view, Employee? me, IEnumerable<string>? teamNames = null)
    {
        using var db = _factory.CreateDbContext();
        var team = teamNames?.ToHashSet() ?? new HashSet<string>();
        var items = new List<CalendarItem>();

        var events = db.CalendarEvents.ToList();
        var leave = db.TimeOffRequests.ToList();
        var docs = db.Documents.ToList();
        var training = db.TrainingRecords.ToList();

        bool isFinanceDeadline(CalendarEvent e) =>
            e.Type == CalendarEventType.Deadline &&
            new[] { "payroll", "invoice", "timesheet", "expense" }
                .Any(k => e.Title.Contains(k, StringComparison.OrdinalIgnoreCase));

        // ---- Company / public events ----
        bool includeCompany = view is CalendarView.My or CalendarView.Team or CalendarView.Company or CalendarView.HRMaster;
        if (includeCompany)
        {
            foreach (var e in events.Where(e => e.Type is CalendarEventType.Holiday or CalendarEventType.Meeting))
                items.Add(new CalendarItem(e.Date, e.Title, e.Type.ToString(), ColorFor(e.Type)));
        }

        // ---- Leave ----
        foreach (var r in leave.Where(r => r.Status != TimeOffStatus.Rejected))
        {
            var include = view switch
            {
                CalendarView.HRMaster => true,
                CalendarView.My => me is not null && r.EmployeeId == me.Id,
                CalendarView.Team => team.Contains(r.EmployeeName),
                _ => false
            };
            if (include)
                items.Add(new CalendarItem(r.StartDate, $"{r.EmployeeName} — {r.Type} leave",
                    $"{r.Type} Leave", LeaveColor(r.Type), r.EmployeeName));
        }

        // ---- Document expiries (passport / visa / permit / contract) ----
        foreach (var d in docs)
        {
            var include = view switch
            {
                CalendarView.HRMaster => true,
                CalendarView.My => me is not null && d.Owner == me.FullName,
                CalendarView.FinanceDueDates => d.Type == "Contract",
                _ => false
            };
            if (include)
            {
                var cat = DocCategory(d);
                items.Add(new CalendarItem(d.ExpiryDate, $"{d.Owner} — {cat}", cat, "#ef4444", d.Owner));
            }
        }

        // ---- Training due ----
        foreach (var t in training.Where(t => t.Status != TrainingStatus.Completed))
        {
            var include = view switch
            {
                CalendarView.HRMaster => true,
                CalendarView.My => me is not null && t.EmployeeName == me.FullName,
                CalendarView.Team => team.Contains(t.EmployeeName),
                _ => false
            };
            if (include)
                items.Add(new CalendarItem(t.DueDate, $"{t.CourseName} due", "Training Due", "#8b5cf6", t.EmployeeName));
        }

        // ---- Finance due dates ----
        if (view is CalendarView.FinanceDueDates or CalendarView.HRMaster)
        {
            foreach (var e in events.Where(isFinanceDeadline))
                items.Add(new CalendarItem(e.Date, e.Title, "Finance Deadline", "#6366f1"));
        }

        // ---- Birthdays / anniversaries ----
        if (view is CalendarView.Company or CalendarView.HRMaster or CalendarView.Team)
        {
            foreach (var e in events.Where(e => e.Type == CalendarEventType.Birthday))
                items.Add(new CalendarItem(e.Date, e.Title, "Birthday / Anniversary", "#ec4899"));
        }

        // ---- Performance review / generic deadlines (company-level) ----
        if (view is CalendarView.HRMaster or CalendarView.Company)
        {
            foreach (var e in events.Where(e => e.Type == CalendarEventType.Deadline && !isFinanceDeadline(e)))
                items.Add(new CalendarItem(e.Date, e.Title, "Deadline", "#f97316"));
        }

        return items.OrderBy(i => i.Date).ToList();
    }

    private static string ColorFor(CalendarEventType type) => type switch
    {
        CalendarEventType.Holiday => "#22c55e",
        CalendarEventType.Meeting => "#3b82f6",
        CalendarEventType.Leave => "#f59e0b",
        CalendarEventType.Training => "#8b5cf6",
        CalendarEventType.Deadline => "#ef4444",
        CalendarEventType.Birthday => "#ec4899",
        _ => "#6b7280"
    };

    private static string LeaveColor(TimeOffType type) => type switch
    {
        TimeOffType.Sick => "#fb923c",
        TimeOffType.Parental => "#0ea5e9",
        TimeOffType.Compassionate => "#a855f7",
        TimeOffType.Unpaid => "#9ca3af",
        _ => "#f59e0b"
    };

    private static string DocCategory(DocumentRecord d)
    {
        if (d.Name.Contains("Passport", StringComparison.OrdinalIgnoreCase)) return "Passport Expiry";
        if (d.Name.Contains("Permit", StringComparison.OrdinalIgnoreCase)) return "Entry Permit Expiry";
        if (d.Type == "Visa" || d.Name.Contains("Visa", StringComparison.OrdinalIgnoreCase)) return "Visa Expiry";
        if (d.Type == "Contract") return "Contract Renewal";
        return "Document Expiry";
    }
}
