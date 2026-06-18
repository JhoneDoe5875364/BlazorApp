using System.ComponentModel.DataAnnotations.Schema;

namespace HCP.HRPortal.Models;

public class Employee
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string Department { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Location { get; set; } = "";
    public DateOnly HireDate { get; set; }
    public string ManagerName { get; set; } = "";
    public string Status { get; set; } = "Active";
    public decimal AnnualSalary { get; set; }
    public int AnnualLeaveTotal { get; set; } = 25;
    public int AnnualLeaveUsed { get; set; }

    /// <summary>Relative URL of the uploaded profile photo (null = use initials avatar).</summary>
    public string? PhotoUrl { get; set; }

    // --- Additional XLSX-sourced fields (optional, may be null for legacy demo employees) ---
    public string? PreferredName { get; set; }
    public string? PersonalEmail { get; set; }
    public DateOnly? Birthday { get; set; }
    public string? EmploymentType { get; set; }            // e.g. "Employee", "Consultant"
    public DateOnly? ProbationEndDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public string? Country { get; set; }

    [NotMapped]
    public int AnnualLeaveRemaining => AnnualLeaveTotal - AnnualLeaveUsed;

    [NotMapped]
    public int YearsOfService =>
        Math.Max(0, (DateOnly.FromDateTime(DateTime.Today).DayNumber - HireDate.DayNumber) / 365);

    [NotMapped]
    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0][..1].ToUpper();
            return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }
    }
}
