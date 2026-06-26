namespace HCP.HRPortal.Models;

public enum TrainingStatus
{
    NotStarted,
    InProgress,
    Completed
}

public class TrainingRecord
{
    public int Id { get; set; }
    public string CourseName { get; set; } = "";
    public string Category { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public TrainingStatus Status { get; set; } = TrainingStatus.NotStarted;
    public int ProgressPercent { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? CompletedOn { get; set; }
    public bool IsMandatory { get; set; }

    /// <summary>SharePoint URL (or local path) of the training document HR uploaded.</summary>
    public string? DocumentUrl { get; set; }
    /// <summary>Filename shown next to the training course in the employee's list.</summary>
    public string? DocumentName { get; set; }
    /// <summary>If true: course is "acknowledge only" — employee just clicks Mark Completed after reading.</summary>
    public bool AcknowledgeOnly { get; set; }
}
