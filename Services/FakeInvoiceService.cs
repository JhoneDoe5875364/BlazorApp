using HCP.HRPortal.Data;
using HCP.HRPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace HCP.HRPortal.Services;

/// <summary>Vendor/supplier invoices, backed by MySQL via EF Core.</summary>
public interface IInvoiceService
{
    IReadOnlyList<Invoice> GetAll();
    void SetStatus(int id, InvoiceStatus status);
    void Add(Invoice invoice);
}

public class FakeInvoiceService : IInvoiceService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public FakeInvoiceService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public IReadOnlyList<Invoice> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.Invoices.OrderBy(i => i.DueDate).ToList();
    }

    public void SetStatus(int id, InvoiceStatus status)
    {
        using var db = _factory.CreateDbContext();
        var inv = db.Invoices.FirstOrDefault(i => i.Id == id);
        if (inv is not null)
        {
            inv.Status = status;
            db.SaveChanges();
        }
    }

    public void Add(Invoice invoice)
    {
        using var db = _factory.CreateDbContext();
        invoice.Id = 0;
        db.Invoices.Add(invoice);
        db.SaveChanges();
    }
}
