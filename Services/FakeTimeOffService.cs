using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

/// <summary>Time-off requests, backed by MySQL via EF Core.</summary>
public class FakeTimeOffService : ITimeOffService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public FakeTimeOffService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public IEnumerable<TimeOffRequest> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.TimeOffRequests.OrderByDescending(r => r.RequestedOn).ToList();
    }

    public IEnumerable<TimeOffRequest> GetForEmployee(int employeeId)
    {
        using var db = _factory.CreateDbContext();
        return db.TimeOffRequests.Where(r => r.EmployeeId == employeeId)
                 .OrderByDescending(r => r.RequestedOn).ToList();
    }

    public IEnumerable<TimeOffRequest> GetPending()
    {
        using var db = _factory.CreateDbContext();
        return db.TimeOffRequests.Where(r => r.Status == TimeOffStatus.Pending)
                 .OrderBy(r => r.StartDate).ToList();
    }

    /// <summary>All requests for a set of employees (e.g. a manager's direct reports).</summary>
    public IEnumerable<TimeOffRequest> GetForEmployees(IEnumerable<int> employeeIds)
    {
        var ids = employeeIds.ToHashSet();
        using var db = _factory.CreateDbContext();
        return db.TimeOffRequests.Where(r => ids.Contains(r.EmployeeId))
                 .OrderByDescending(r => r.RequestedOn).ToList();
    }

    public int PendingCount
    {
        get { using var db = _factory.CreateDbContext(); return db.TimeOffRequests.Count(r => r.Status == TimeOffStatus.Pending); }
    }

    public void Add(TimeOffRequest request)
    {
        using var db = _factory.CreateDbContext();
        request.Id = 0;
        request.RequestedOn = DateOnly.FromDateTime(DateTime.Today);
        request.Status = TimeOffStatus.Pending;
        db.TimeOffRequests.Add(request);
        db.SaveChanges();
    }

    public void SetStatus(int id, TimeOffStatus status)
    {
        using var db = _factory.CreateDbContext();
        var req = db.TimeOffRequests.FirstOrDefault(r => r.Id == id);
        if (req is not null)
        {
            req.Status = status;
            db.SaveChanges();
        }
    }
}
