namespace HCP.HRPortal.Models;

public enum CalendarEventType
{
    Holiday,
    Meeting,
    Leave,
    Training,
    Deadline,
    Birthday
}

/// <summary>
/// Who can see an event.
///   Personal — only the creator (shown in their "My Calendar").
///   Team     — creator + their direct team / subordinates.
///   Company  — everyone (holidays, town halls, system-wide deadlines).
/// </summary>
public enum CalendarEventScope
{
    Personal,
    Team,
    Company,
}

public class CalendarEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateOnly Date { get; set; }
    public CalendarEventType Type { get; set; } = CalendarEventType.Meeting;
    public string Description { get; set; } = "";
    /// <summary>Country code for holidays (e.g. "UAE", "US", "UK"). Null for non-holiday events.</summary>
    public string? Country { get; set; }

    /// <summary>Visibility scope — drives which calendar tabs the event appears in.</summary>
    public CalendarEventScope Scope { get; set; } = CalendarEventScope.Company;
    /// <summary>Email of the user who created this event (or "(system)" for seeded data).</summary>
    public string CreatedBy { get; set; } = "(system)";
}
