using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

/// <summary>
/// Employee roster, now backed by the MySQL database via EF Core.
/// (Kept the original type name so existing pages' <c>@inject</c> lines keep working.)
/// </summary>
public class FakeEmployeeService : IEmployeeService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public FakeEmployeeService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public IReadOnlyList<Employee> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.Employees.OrderBy(e => e.FullName).ToList();
    }

    public Employee? GetById(int id)
    {
        using var db = _factory.CreateDbContext();
        return db.Employees.FirstOrDefault(e => e.Id == id);
    }

    public Employee? GetByEmail(string email)
    {
        using var db = _factory.CreateDbContext();
        return db.Employees.FirstOrDefault(e => e.Email == email);
    }

    public void Add(Employee employee)
    {
        using var db = _factory.CreateDbContext();
        employee.Id = 0;
        db.Employees.Add(employee);
        db.SaveChanges();
    }

    public void Update(Employee employee)
    {
        using var db = _factory.CreateDbContext();
        var e = db.Employees.FirstOrDefault(x => x.Id == employee.Id);
        if (e is null) return;
        e.FullName = employee.FullName;
        e.JobTitle = employee.JobTitle;
        e.Department = employee.Department;
        e.Email = employee.Email;
        e.Phone = employee.Phone;
        e.Location = employee.Location;
        e.ManagerName = employee.ManagerName;
        e.Status = employee.Status;
        e.HireDate = employee.HireDate;
        db.SaveChanges();
    }

    public void Delete(int id)
    {
        using var db = _factory.CreateDbContext();
        var e = db.Employees.FirstOrDefault(x => x.Id == id);
        if (e is null) return;
        db.Employees.Remove(e);
        db.SaveChanges();
    }

    /// <summary>Persists the uploaded profile photo URL for an employee.</summary>
    public void SetPhoto(int employeeId, string photoUrl)
    {
        using var db = _factory.CreateDbContext();
        var e = db.Employees.FirstOrDefault(x => x.Id == employeeId);
        if (e is not null)
        {
            e.PhotoUrl = photoUrl;
            db.SaveChanges();
        }
    }

    /// <summary>Employees who report directly to the named manager (role-based scoping).</summary>
    public IReadOnlyList<Employee> GetDirectReports(string managerName)
    {
        using var db = _factory.CreateDbContext();
        return db.Employees.Where(e => e.ManagerName == managerName)
                 .OrderBy(e => e.FullName).ToList();
    }

    public int HeadCount
    {
        get { using var db = _factory.CreateDbContext(); return db.Employees.Count(); }
    }

    public int OnLeaveCount
    {
        get { using var db = _factory.CreateDbContext(); return db.Employees.Count(e => e.Status == "On Leave"); }
    }

    public IEnumerable<IGrouping<string, Employee>> ByDepartment()
    {
        using var db = _factory.CreateDbContext();
        return db.Employees.OrderBy(e => e.Department).ToList()
                 .GroupBy(e => e.Department)
                 .OrderBy(g => g.Key)
                 .ToList();
    }

    public IEnumerable<string> Departments()
    {
        using var db = _factory.CreateDbContext();
        return db.Employees.Select(e => e.Department).Distinct().OrderBy(d => d).ToList();
    }
}
