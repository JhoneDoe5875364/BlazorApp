namespace HCP.HRPortal.Models;

/// <summary>A piece of feedback left for an employee by a manager, peer, or skip-level.</summary>
public class PerformanceFeedback
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }              // the employee the feedback is about
    public string EmployeeName { get; set; } = "";
    public string Author { get; set; } = "";
    public string Relation { get; set; } = "";       // Manager, Peer, Skip Manager…
    public string Comment { get; set; } = "";
    public DateOnly Date { get; set; }
}
