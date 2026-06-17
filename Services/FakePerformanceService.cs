using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

/// <summary>Performance reviews, goals, and feedback — backed by MySQL via EF Core.</summary>
public interface IPerformanceService
{
    IReadOnlyList<PerformanceReview> GetAll();
    IReadOnlyList<PerformanceReview> GetForEmployees(IEnumerable<int> employeeIds);
    PerformanceReview? GetReviewForEmployee(int employeeId);
    IReadOnlyList<PerformanceGoal> GetGoalsForEmployee(int employeeId);
    IReadOnlyList<PerformanceFeedback> GetFeedbackForEmployee(int employeeId);
}

public class FakePerformanceService : IPerformanceService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public FakePerformanceService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public IReadOnlyList<PerformanceReview> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.PerformanceReviews.OrderBy(r => r.DueDate).ToList();
    }

    public IReadOnlyList<PerformanceReview> GetForEmployees(IEnumerable<int> employeeIds)
    {
        var ids = employeeIds.ToHashSet();
        using var db = _factory.CreateDbContext();
        return db.PerformanceReviews.Where(r => ids.Contains(r.EmployeeId)).ToList();
    }

    public PerformanceReview? GetReviewForEmployee(int employeeId)
    {
        using var db = _factory.CreateDbContext();
        return db.PerformanceReviews
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.DueDate)
            .FirstOrDefault();
    }

    public IReadOnlyList<PerformanceGoal> GetGoalsForEmployee(int employeeId)
    {
        using var db = _factory.CreateDbContext();
        return db.PerformanceGoals.Where(g => g.EmployeeId == employeeId).ToList();
    }

    public IReadOnlyList<PerformanceFeedback> GetFeedbackForEmployee(int employeeId)
    {
        using var db = _factory.CreateDbContext();
        return db.PerformanceFeedback
            .Where(f => f.EmployeeId == employeeId)
            .OrderByDescending(f => f.Date)
            .ToList();
    }
}
