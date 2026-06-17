using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

/// <summary>Business-travel requests, backed by MySQL via EF Core.</summary>
public interface ITravelService
{
    IReadOnlyList<BusinessTrip> GetAll();
    IReadOnlyList<BusinessTrip> GetForEmployee(int employeeId);
    IReadOnlyList<BusinessTrip> GetForEmployees(IEnumerable<int> employeeIds);
    void Add(BusinessTrip trip);
    void SetStatus(int id, TripStatus status);
}

public class FakeTravelService : ITravelService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public FakeTravelService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public IReadOnlyList<BusinessTrip> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.BusinessTrips.OrderBy(t => t.DepartDate).ToList();
    }

    public IReadOnlyList<BusinessTrip> GetForEmployee(int employeeId)
    {
        using var db = _factory.CreateDbContext();
        return db.BusinessTrips
            .Where(t => t.EmployeeId == employeeId)
            .OrderBy(t => t.DepartDate)
            .ToList();
    }

    public IReadOnlyList<BusinessTrip> GetForEmployees(IEnumerable<int> employeeIds)
    {
        var ids = employeeIds.ToHashSet();
        using var db = _factory.CreateDbContext();
        return db.BusinessTrips.Where(t => ids.Contains(t.EmployeeId)).ToList();
    }

    public void Add(BusinessTrip trip)
    {
        using var db = _factory.CreateDbContext();
        trip.Id = 0;
        db.BusinessTrips.Add(trip);
        db.SaveChanges();
    }

    public void SetStatus(int id, TripStatus status)
    {
        using var db = _factory.CreateDbContext();
        var trip = db.BusinessTrips.FirstOrDefault(t => t.Id == id);
        if (trip is not null)
        {
            trip.Status = status;
            db.SaveChanges();
        }
    }
}
