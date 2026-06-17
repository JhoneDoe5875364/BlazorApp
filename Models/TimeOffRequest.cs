using System.ComponentModel.DataAnnotations.Schema;

namespace HCP.HRPortal.Models;

public enum TimeOffType
{
    Annual,
    Sick,
    Unpaid,
    Parental,
    Compassionate
}

public enum TimeOffStatus
{
    Pending,
    Approved,
    Rejected
}

public class TimeOffRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public TimeOffType Type { get; set; } = TimeOffType.Annual;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = "";
    public TimeOffStatus Status { get; set; } = TimeOffStatus.Pending;
    public DateOnly RequestedOn { get; set; }

    /// <summary>Relative URL of an optional supporting document (e.g. medical note).</summary>
    public string? AttachmentUrl { get; set; }
    /// <summary>Original filename shown to the user for the attachment link.</summary>
    public string? AttachmentName { get; set; }

    [NotMapped]
    public int Days => Math.Max(1, EndDate.DayNumber - StartDate.DayNumber + 1);
}
