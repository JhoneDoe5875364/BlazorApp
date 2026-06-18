using HCP.HRPortal.Models;

namespace HCP.HRPortal.Data;

/// <summary>
/// The four portal roles. Each role lands on its own dashboard and sees a
/// role-appropriate navigation menu.
/// </summary>
public static class Roles
{
    public const string Employee = "Employee";
    public const string Manager = "Manager";
    public const string HR = "HR";
    public const string Finance = "Finance";

    public static readonly string[] All = { Employee, Manager, HR, Finance };

    /// <summary>Landing dashboard route for each role.</summary>
    public static string DashboardFor(string role) => role switch
    {
        Manager => "/management-dashboard",
        HR => "/hr-dashboard",
        Finance => "/finance-dashboard",
        _ => "/employee-dashboard",
    };

    /// <summary>
    /// Derives the portal role from an employee's department and job title.
    /// Used both by DB seeding and by manual / bulk employee creation flows.
    /// </summary>
    public static string For(Employee e) => For(e.Department, e.JobTitle);

    /// <summary>Department/title overload — used by import flows that may not have a full Employee yet.</summary>
    public static string For(string? department, string? jobTitle)
    {
        var dept = (department ?? "").Trim();
        var title = (jobTitle ?? "").Trim();

        // HR / Finance / Accounting departments map to fixed roles regardless of title.
        if (dept.Equals("Human Resources", StringComparison.OrdinalIgnoreCase))
            return HR;
        if (dept.Equals("Finance", StringComparison.OrdinalIgnoreCase)
            || dept.Equals("Accounting", StringComparison.OrdinalIgnoreCase))
            return Finance;

        // Anyone in Executive is a manager (CEO/COO/etc.).
        if (dept.Equals("Executive", StringComparison.OrdinalIgnoreCase))
            return Manager;

        // Title-based leadership detection (covers "MGR" suffix used in the XLSX too).
        string[] leadership = { "Manager", "MGR", "Director", "Head", "VP", "Chief", "Lead" };
        if (leadership.Any(k => title.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return Manager;

        return Employee;
    }

    /// <summary>
    /// Demo account email per role, used by the prototype "View as" role switcher.
    /// (The switcher re-signs-in as one of these seeded accounts — Development only.)
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DemoAccount = new Dictionary<string, string>
    {
        [Employee] = "sara.almansouri@hcp.example",
        [Manager]  = "daniel.okafor@hcp.example",
        [HR]       = "grace.chen@hcp.example",
        [Finance]  = "yusuf.demir@hcp.example",
    };
}
