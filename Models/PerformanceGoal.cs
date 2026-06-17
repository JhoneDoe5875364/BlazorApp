namespace HCP.HRPortal.Models;

public enum GoalStatus { OnTrack, AtRisk, Behind, Completed }

/// <summary>A single objective for an employee within a review cycle.</summary>
public class PerformanceGoal
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string Cycle { get; set; } = "";          // e.g. "Q2 2026"
    public string Title { get; set; } = "";
    public int ProgressPercent { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.OnTrack;
}
