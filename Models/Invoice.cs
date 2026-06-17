namespace HCP.HRPortal.Models;

public enum InvoiceStatus { Unpaid, Paid, Overdue }

public class Invoice
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public string Vendor { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;
}
