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
}
