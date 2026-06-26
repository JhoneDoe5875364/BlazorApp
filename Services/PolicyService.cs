using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

/// <summary>
/// Company policies + per-user acknowledgements, persisted in MySQL via EF Core.
/// The first 9 policies are seeded by <see cref="DbInitializer"/> on a fresh DB so
/// the page is not empty on first boot; HR may then add / edit / delete from the UI.
/// </summary>
public interface IPolicyService
{
    IReadOnlyList<CompanyPolicy> All { get; }
    CompanyPolicy? Get(int id);
    bool IsAcknowledged(string userEmail, int policyId);
    DateOnly? AcknowledgedOn(string userEmail, int policyId);
    void Acknowledge(string userEmail, int policyId);
    void Add(CompanyPolicy policy);
    void Update(CompanyPolicy policy);
    void Delete(int policyId);
}

public class PolicyService : IPolicyService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    public PolicyService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public IReadOnlyList<CompanyPolicy> All
    {
        get
        {
            using var db = _factory.CreateDbContext();
            return db.CompanyPolicies.OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();
        }
    }

    public CompanyPolicy? Get(int id)
    {
        using var db = _factory.CreateDbContext();
        return db.CompanyPolicies.FirstOrDefault(p => p.Id == id);
    }

    public bool IsAcknowledged(string userEmail, int policyId)
    {
        if (string.IsNullOrWhiteSpace(userEmail)) return false;
        using var db = _factory.CreateDbContext();
        return db.PolicyAcknowledgements.Any(a => a.PolicyId == policyId && a.UserEmail == userEmail);
    }

    public DateOnly? AcknowledgedOn(string userEmail, int policyId)
    {
        if (string.IsNullOrWhiteSpace(userEmail)) return null;
        using var db = _factory.CreateDbContext();
        var ack = db.PolicyAcknowledgements
            .FirstOrDefault(a => a.PolicyId == policyId && a.UserEmail == userEmail);
        return ack?.AcknowledgedOn;
    }

    public void Acknowledge(string userEmail, int policyId)
    {
        if (string.IsNullOrWhiteSpace(userEmail)) return;
        using var db = _factory.CreateDbContext();
        if (db.PolicyAcknowledgements.Any(a => a.PolicyId == policyId && a.UserEmail == userEmail))
            return;
        db.PolicyAcknowledgements.Add(new PolicyAcknowledgement
        {
            PolicyId = policyId,
            UserEmail = userEmail,
            AcknowledgedOn = DateOnly.FromDateTime(DateTime.Today),
        });
        db.SaveChanges();
    }

    public void Add(CompanyPolicy policy)
    {
        using var db = _factory.CreateDbContext();
        policy.Id = 0;
        if (string.IsNullOrWhiteSpace(policy.Updated))
            policy.Updated = DateTime.Today.ToString("dd MMM yyyy");
        db.CompanyPolicies.Add(policy);
        db.SaveChanges();
    }

    public void Update(CompanyPolicy policy)
    {
        using var db = _factory.CreateDbContext();
        var existing = db.CompanyPolicies.FirstOrDefault(p => p.Id == policy.Id);
        if (existing is null) return;
        existing.Name = policy.Name;
        existing.Category = policy.Category;
        existing.Version = policy.Version;
        existing.Updated = string.IsNullOrWhiteSpace(policy.Updated)
            ? DateTime.Today.ToString("dd MMM yyyy")
            : policy.Updated;
        existing.Mandatory = policy.Mandatory;
        existing.DocumentUrl = policy.DocumentUrl;
        db.SaveChanges();
    }

    public void Delete(int policyId)
    {
        using var db = _factory.CreateDbContext();
        var existing = db.CompanyPolicies.FirstOrDefault(p => p.Id == policyId);
        if (existing is null) return;
        // Cascade delete acks too
        var acks = db.PolicyAcknowledgements.Where(a => a.PolicyId == policyId).ToList();
        if (acks.Count > 0) db.PolicyAcknowledgements.RemoveRange(acks);
        db.CompanyPolicies.Remove(existing);
        db.SaveChanges();
    }
}
