using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

/// <summary>
/// Document register, backed by MySQL via EF Core. Note that <see cref="DocumentRecord.Status"/>
/// is a computed property (not a column), so status filtering is done in memory after loading.
/// </summary>
public class FakeDocumentService : IDocumentService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public FakeDocumentService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public IEnumerable<DocumentRecord> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.Documents.OrderBy(d => d.ExpiryDate).ToList();
    }

    public IEnumerable<DocumentRecord> GetForOwner(string owner)
    {
        using var db = _factory.CreateDbContext();
        return db.Documents.Where(d => d.Owner == owner).OrderBy(d => d.ExpiryDate).ToList();
    }

    public IEnumerable<DocumentRecord> GetByType(string type)
    {
        using var db = _factory.CreateDbContext();
        return db.Documents.Where(d => d.Type == type).OrderBy(d => d.ExpiryDate).ToList();
    }

    public IEnumerable<DocumentRecord> GetExpiring()
    {
        using var db = _factory.CreateDbContext();
        return db.Documents.OrderBy(d => d.ExpiryDate).ToList()
                 .Where(d => d.Status != DocumentStatus.Valid)
                 .ToList();
    }

    public int ExpiringSoonCount
    {
        get
        {
            using var db = _factory.CreateDbContext();
            return db.Documents.ToList().Count(d => d.Status == DocumentStatus.ExpiringSoon);
        }
    }

    public int ExpiredCount
    {
        get
        {
            using var db = _factory.CreateDbContext();
            return db.Documents.ToList().Count(d => d.Status == DocumentStatus.Expired);
        }
    }

    // Expiry counts by category (computed in memory since Status/DaysUntilExpiry are not columns).
    public int VisaExpiringSoon =>
        CountWhere(d => (d.Type == "Visa" || d.Name.Contains("Visa") || d.Name.Contains("Permit")) && d.DaysUntilExpiry is >= 0 and <= 90);

    public int PassportExpiringSoon =>
        CountWhere(d => d.Name.Contains("Passport") && d.DaysUntilExpiry is >= 0 and <= 180);

    public int ContractsDue =>
        CountWhere(d => d.Type == "Contract" && d.DaysUntilExpiry <= 120);

    private int CountWhere(Func<DocumentRecord, bool> predicate)
    {
        using var db = _factory.CreateDbContext();
        return db.Documents.ToList().Count(predicate);
    }

    /// <summary>Finance-relevant documents (contracts, insurance) — for the Finance document view.</summary>
    public IEnumerable<DocumentRecord> GetFinanceDocuments()
    {
        using var db = _factory.CreateDbContext();
        return db.Documents.Where(d => d.Type == "Contract" || d.Type == "Insurance")
                 .OrderBy(d => d.ExpiryDate).ToList();
    }

    public void Add(DocumentRecord doc)
    {
        using var db = _factory.CreateDbContext();
        doc.Id = 0;
        db.Documents.Add(doc);
        db.SaveChanges();
    }
}
