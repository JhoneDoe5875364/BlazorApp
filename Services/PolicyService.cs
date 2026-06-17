namespace HCP.HRPortal.Services;

public record CompanyPolicy(int Id, string Name, string Category, string Version, string Updated, bool Mandatory);

/// <summary>
/// Company policies + per-user acknowledgements. In-memory singleton (prototype): a user's
/// acknowledgements persist for the app's lifetime. Swap for a DB-backed impl later.
/// </summary>
public interface IPolicyService
{
    IReadOnlyList<CompanyPolicy> All { get; }
    bool IsAcknowledged(string userEmail, int policyId);
    DateOnly? AcknowledgedOn(string userEmail, int policyId);
    void Acknowledge(string userEmail, int policyId);
}

public class PolicyService : IPolicyService
{
    private readonly object _lock = new();
    private readonly Dictionary<(string Email, int PolicyId), DateOnly> _acks = new();
    private readonly HashSet<string> _seeded = new();

    private readonly List<CompanyPolicy> _policies = new()
    {
        new(1, "Employee Handbook", "HR", "v2.0", "01 May 2026", true),
        new(2, "Code of Conduct", "Compliance", "v1.3", "25 Apr 2026", true),
        new(3, "IT Security Policy", "IT", "v2.1", "01 May 2026", true),
        new(4, "Travel & Expense Policy", "Finance", "v1.2", "12 May 2026", false),
        new(5, "Data Privacy & GDPR", "Compliance", "v3.0", "10 Apr 2026", true),
        new(6, "Health & Safety", "HSE", "v1.5", "20 Mar 2026", true),
        new(7, "Remote Work Policy", "HR", "v1.0", "15 Feb 2026", false),
        new(8, "Anti-Bribery Policy", "Compliance", "v2.2", "30 Jan 2026", true),
    };

    public IReadOnlyList<CompanyPolicy> All => _policies;

    public bool IsAcknowledged(string userEmail, int policyId)
    {
        EnsureSeeded(userEmail);
        lock (_lock) return _acks.ContainsKey((userEmail, policyId));
    }

    public DateOnly? AcknowledgedOn(string userEmail, int policyId)
    {
        EnsureSeeded(userEmail);
        lock (_lock) return _acks.TryGetValue((userEmail, policyId), out var d) ? d : null;
    }

    public void Acknowledge(string userEmail, int policyId)
    {
        if (string.IsNullOrWhiteSpace(userEmail)) return;
        lock (_lock) _acks[(userEmail, policyId)] = DateOnly.FromDateTime(DateTime.Today);
    }

    // Pre-acknowledge a default set so the demo starts with realistic numbers.
    private void EnsureSeeded(string userEmail)
    {
        if (string.IsNullOrWhiteSpace(userEmail)) return;
        lock (_lock)
        {
            if (!_seeded.Add(userEmail)) return;
            foreach (var id in new[] { 1, 2, 3, 4, 6, 7 })
                _acks[(userEmail, id)] = new DateOnly(2026, 4, 20);
        }
    }
}
