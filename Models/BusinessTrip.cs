namespace HCP.HRPortal.Models;

public enum TripStatus { Pending, Approved, Completed, Rejected }

/// <summary>A business travel request / trip for an employee.</summary>
public class BusinessTrip
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateOnly DepartDate { get; set; }
    public DateOnly ReturnDate { get; set; }
    public decimal EstimatedCost { get; set; }
    public TripStatus Status { get; set; } = TripStatus.Pending;
}
