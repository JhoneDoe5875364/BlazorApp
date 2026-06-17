using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

/// <summary>Salary-certificate requests, backed by MySQL via EF Core.</summary>
public interface ISalaryCertificateService
{
    IReadOnlyList<SalaryCertificateRequest> GetAll();
    IReadOnlyList<SalaryCertificateRequest> GetForEmployee(int employeeId);
    int PendingCount();                                  // Pending + Processing (Finance KPI)
    void Add(SalaryCertificateRequest request);
    void SetStatus(int id, CertificateStatus status);
}

public class FakeSalaryCertificateService : ISalaryCertificateService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public FakeSalaryCertificateService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public IReadOnlyList<SalaryCertificateRequest> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.SalaryCertificateRequests.OrderByDescending(r => r.RequestedOn).ToList();
    }

    public IReadOnlyList<SalaryCertificateRequest> GetForEmployee(int employeeId)
    {
        using var db = _factory.CreateDbContext();
        return db.SalaryCertificateRequests
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.RequestedOn)
            .ToList();
    }

    public int PendingCount()
    {
        using var db = _factory.CreateDbContext();
        return db.SalaryCertificateRequests
            .Count(r => r.Status == CertificateStatus.Pending || r.Status == CertificateStatus.Processing);
    }

    public void Add(SalaryCertificateRequest request)
    {
        using var db = _factory.CreateDbContext();
        request.Id = 0;
        db.SalaryCertificateRequests.Add(request);
        db.SaveChanges();
    }

    public void SetStatus(int id, CertificateStatus status)
    {
        using var db = _factory.CreateDbContext();
        var req = db.SalaryCertificateRequests.FirstOrDefault(r => r.Id == id);
        if (req is not null)
        {
            req.Status = status;
            db.SaveChanges();
        }
    }
}
