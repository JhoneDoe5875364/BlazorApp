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
}
