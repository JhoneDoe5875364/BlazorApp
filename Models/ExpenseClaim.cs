namespace HCP.HRPortal.Models;

public enum ExpenseStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Reimbursed
}

public class ExpenseClaim
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "Travel";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateOnly Date { get; set; }
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Submitted;
    public string SubmittedBy { get; set; } = "";
    public string Notes { get; set; } = "";
}
