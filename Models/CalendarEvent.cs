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

public class CalendarEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateOnly Date { get; set; }
    public CalendarEventType Type { get; set; } = CalendarEventType.Meeting;
    public string Description { get; set; } = "";
}
