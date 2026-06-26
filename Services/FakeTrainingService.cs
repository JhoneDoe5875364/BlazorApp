using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

/// <summary>Training records, backed by MySQL via EF Core.</summary>
public class FakeTrainingService : ITrainingService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public FakeTrainingService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public IEnumerable<TrainingRecord> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.TrainingRecords.OrderBy(r => r.DueDate).ToList();
    }

    public IEnumerable<TrainingRecord> GetForEmployee(string name)
    {
        using var db = _factory.CreateDbContext();
        return db.TrainingRecords.Where(r => r.EmployeeName == name)
                 .OrderBy(r => r.DueDate).ToList();
    }

    public int MandatoryOutstanding
    {
        get
        {
            using var db = _factory.CreateDbContext();
            return db.TrainingRecords.Count(r => r.IsMandatory && r.Status != TrainingStatus.Completed);
        }
    }

    public void SetProgress(int id, int percent)
    {
        using var db = _factory.CreateDbContext();
        var r = db.TrainingRecords.FirstOrDefault(x => x.Id == id);
        if (r is null) return;

        r.ProgressPercent = Math.Clamp(percent, 0, 100);
        r.Status = r.ProgressPercent >= 100 ? TrainingStatus.Completed
                 : r.ProgressPercent > 0 ? TrainingStatus.InProgress
                 : TrainingStatus.NotStarted;
        r.CompletedOn = r.Status == TrainingStatus.Completed ? DateOnly.FromDateTime(DateTime.Today) : null;
        db.SaveChanges();
    }

    public int DistributeToEmployees(
        string courseName, string category, bool mandatory, bool acknowledgeOnly,
        DateOnly dueDate, string? documentUrl, string? documentName,
        IEnumerable<string>? forEmployeeNames = null)
    {
        using var db = _factory.CreateDbContext();
        var targets = forEmployeeNames?.ToList()
            ?? db.Employees.Where(e => e.Status != "Inactive").Select(e => e.FullName).ToList();

        int added = 0;
        foreach (var name in targets)
        {
            // Skip if this employee already has this course assigned
            if (db.TrainingRecords.Any(r => r.EmployeeName == name && r.CourseName == courseName))
                continue;

            db.TrainingRecords.Add(new TrainingRecord
            {
                CourseName = courseName,
                Category = category,
                EmployeeName = name,
                Status = TrainingStatus.NotStarted,
                ProgressPercent = 0,
                DueDate = dueDate,
                IsMandatory = mandatory,
                DocumentUrl = documentUrl,
                DocumentName = documentName,
                AcknowledgeOnly = acknowledgeOnly,
            });
            added++;
        }
        if (added > 0) db.SaveChanges();
        return added;
    }
}
