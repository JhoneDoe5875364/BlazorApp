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
