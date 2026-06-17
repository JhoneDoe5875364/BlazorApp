using Microsoft.AspNetCore.Identity;
using HCP.HRPortal.Models;

namespace HCP.HRPortal.Data;

/// <summary>
/// Identity login account. Every employee gets one of these so they can sign in.
/// The account is linked 1:1 to an <see cref="Employee"/> profile via <see cref="EmployeeId"/>,
/// and the user's role (Employee / Manager / HR / Finance) decides which dashboard they land on.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>FK to the HR <see cref="Employee"/> record this login belongs to.</summary>
    public int? EmployeeId { get; set; }

    public Employee? Employee { get; set; }
}
