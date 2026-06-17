namespace HCP.HRPortal.Models;

/// <summary>How an employee is tracking against expectations for a review cycle.</summary>
public enum PerformanceBand { BelowExpectations, MeetsExpectations, AboveExpectations }

/// <summary>A per-employee performance review for one cycle (e.g. "Q2 2026").</summary>
public class PerformanceReview
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string Cycle { get; set; } = "";          // e.g. "Q2 2026"
    public double Rating { get; set; }                // 0–5
    public PerformanceBand Band { get; set; } = PerformanceBand.MeetsExpectations;
    public DateOnly DueDate { get; set; }             // when the review must be completed
    public bool Completed { get; set; }
}
