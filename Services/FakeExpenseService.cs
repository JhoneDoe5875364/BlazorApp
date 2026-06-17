using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

/// <summary>Expense claims, backed by MySQL via EF Core.</summary>
public class FakeExpenseService : IExpenseService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public FakeExpenseService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public IEnumerable<ExpenseClaim> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.ExpenseClaims.OrderByDescending(c => c.Date).ToList();
    }

    public IEnumerable<ExpenseClaim> GetForUser(string name)
    {
        using var db = _factory.CreateDbContext();
        return db.ExpenseClaims.Where(c => c.SubmittedBy == name)
                 .OrderByDescending(c => c.Date).ToList();
    }

    public IEnumerable<ExpenseClaim> GetByStatus(ExpenseStatus status)
    {
        using var db = _factory.CreateDbContext();
        return db.ExpenseClaims.Where(c => c.Status == status).ToList();
    }

    public decimal TotalSubmitted
    {
        get
        {
            using var db = _factory.CreateDbContext();
            return db.ExpenseClaims
                     .Where(c => c.Status == ExpenseStatus.Submitted || c.Status == ExpenseStatus.Approved)
                     .Sum(c => (decimal?)c.Amount) ?? 0m;
        }
    }

    public decimal TotalReimbursed
    {
        get
        {
            using var db = _factory.CreateDbContext();
            return db.ExpenseClaims.Where(c => c.Status == ExpenseStatus.Reimbursed)
                     .Sum(c => (decimal?)c.Amount) ?? 0m;
        }
    }

    public int PendingCount
    {
        get { using var db = _factory.CreateDbContext(); return db.ExpenseClaims.Count(c => c.Status == ExpenseStatus.Submitted); }
    }

    public void Add(ExpenseClaim claim)
    {
        using var db = _factory.CreateDbContext();
        claim.Id = 0;
        claim.Status = ExpenseStatus.Submitted;
        db.ExpenseClaims.Add(claim);
        db.SaveChanges();
    }

    public void SetStatus(int id, ExpenseStatus status)
    {
        using var db = _factory.CreateDbContext();
        var claim = db.ExpenseClaims.FirstOrDefault(c => c.Id == id);
        if (claim is not null)
        {
            claim.Status = status;
            db.SaveChanges();
        }
    }
}
