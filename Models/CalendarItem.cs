namespace HCP.HRPortal.Models;

/// <summary>
/// Which role-filtered calendar the user is looking at (see brief §7).
/// </summary>
public enum CalendarView
{
    My,              // personal items + company events
    Team,            // a manager's direct reports
    HRMaster,        // HR sees everything
    FinanceDueDates, // finance reminders (payroll, invoices, timesheet/expense deadlines)
    Company          // company-wide public events
}

/// <summary>
/// A single, source-agnostic calendar entry. Aggregated from several tables
/// (events, leave, document expiries, training) and tagged with a category so the
/// calendar can colour-code and filter by role.
/// </summary>
public record CalendarItem(
    DateOnly Date,
    string Title,
    string Category,
    string Color,
    string? Person = null);
