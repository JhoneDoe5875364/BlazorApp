using HCP.HRPortal.Data;
using HCP.HRPortal.Models;

namespace HCP.HRPortal.Services;

/// <summary>
/// App-wide notification hub. Singleton + a <see cref="Changed"/> event: when a notification
/// is added/read, every connected Blazor circuit whose user is affected re-renders its bell
/// badge live over the existing SignalR connection (no polling, no refresh).
/// Notifications are derived per user AND per role on first access (option A) so there is
/// something to see in development; each item carries a <see cref="NotificationItem.Link"/>
/// so clicking it opens the page where the user can resolve it.
/// </summary>
public interface INotificationService
{
    IReadOnlyList<NotificationItem> GetFor(string userEmail, IEnumerable<string> roles);
    int UnreadCount(string userEmail, IEnumerable<string> roles);
    void Add(NotificationItem item);
    void MarkRead(Guid id);
    void MarkAllRead(string userEmail);

    // Action-based notifications (target the counterpart, never the actor).
    void NotifyTimeOffSubmitted(Employee requester, TimeOffRequest req);
    void NotifyExpenseSubmitted(Employee requester, ExpenseClaim claim);
    void NotifyLeaveDecision(TimeOffRequest req, string deciderEmail, bool approved);
    void NotifyExpenseDecision(ExpenseClaim claim, string deciderEmail, ExpenseStatus status);

    /// <summary>Raised with the affected recipient email whenever notifications change.</summary>
    event Action<string>? Changed;
}

public class NotificationService : INotificationService
{
    private readonly object _lock = new();
    private readonly List<NotificationItem> _items = new();
    private readonly HashSet<string> _seeded = new();

    private readonly IEmployeeService _employees;
    private readonly IDocumentService _documents;
    private readonly ITrainingService _training;
    private readonly ITimeOffService _timeOff;
    private readonly IExpenseService _expenses;

    public event Action<string>? Changed;

    public NotificationService(IEmployeeService employees, IDocumentService documents,
        ITrainingService training, ITimeOffService timeOff, IExpenseService expenses)
    {
        _employees = employees;
        _documents = documents;
        _training = training;
        _timeOff = timeOff;
        _expenses = expenses;
    }

    public IReadOnlyList<NotificationItem> GetFor(string userEmail, IEnumerable<string> roles)
    {
        EnsureSeeded(userEmail, roles);
        lock (_lock)
            return _items.Where(n => n.Recipient == userEmail)
                         .OrderByDescending(n => n.CreatedAt)
                         .ToList();
    }

    public int UnreadCount(string userEmail, IEnumerable<string> roles)
    {
        EnsureSeeded(userEmail, roles);
        lock (_lock)
            return _items.Count(n => n.Recipient == userEmail && !n.IsRead);
    }

    public void Add(NotificationItem item)
    {
        lock (_lock) _items.Add(item);
        Changed?.Invoke(item.Recipient);
    }

    public void MarkRead(Guid id)
    {
        string? recipient = null;
        lock (_lock)
        {
            var n = _items.FirstOrDefault(x => x.Id == id);
            if (n is not null && !n.IsRead) { n.IsRead = true; recipient = n.Recipient; }
        }
        if (recipient is not null) Changed?.Invoke(recipient);
    }

    public void MarkAllRead(string userEmail)
    {
        lock (_lock)
            foreach (var n in _items.Where(n => n.Recipient == userEmail))
                n.IsRead = true;
        Changed?.Invoke(userEmail);
    }

    // ---- Action-based notifications -------------------------------------------
    public void NotifyTimeOffSubmitted(Employee requester, TimeOffRequest req)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var managerEmail = ManagerEmailOf(requester);
        if (managerEmail is not null) recipients.Add(managerEmail);
        foreach (var hr in EmailsInDepartment("Human Resources")) recipients.Add(hr);
        recipients.Remove(requester.Email); // never notify the actor

        foreach (var to in recipients)
            Add(new NotificationItem
            {
                Recipient = to,
                Title = $"Leave request from {requester.FullName}",
                Message = $"{req.Type} leave {req.StartDate:dd MMM} – {req.EndDate:dd MMM} awaiting approval.",
                Icon = "bi-airplane-fill",
                Tone = "info",
                Link = "/approvals"
            });
    }

    public void NotifyExpenseSubmitted(Employee requester, ExpenseClaim claim)
    {
        var recipients = new HashSet<string>(EmailsInDepartment("Finance"), StringComparer.OrdinalIgnoreCase);
        recipients.Remove(requester.Email);

        foreach (var to in recipients)
            Add(new NotificationItem
            {
                Recipient = to,
                Title = $"Expense claim from {requester.FullName}",
                Message = $"{claim.Title} · {claim.Currency} {claim.Amount:N0} — awaiting review.",
                Icon = "bi-receipt",
                Tone = "warning",
                Link = "/finance-dashboard"
            });
    }

    public void NotifyLeaveDecision(TimeOffRequest req, string deciderEmail, bool approved)
    {
        var requester = _employees.GetById(req.EmployeeId);
        if (requester is null) return;
        if (string.Equals(requester.Email, deciderEmail, StringComparison.OrdinalIgnoreCase)) return; // actor == requester

        Add(new NotificationItem
        {
            Recipient = requester.Email,
            Title = approved ? "Leave request approved" : "Leave request rejected",
            Message = $"Your {req.Type} leave {req.StartDate:dd MMM} – {req.EndDate:dd MMM} was {(approved ? "approved" : "rejected")}.",
            Icon = approved ? "bi-check-circle-fill" : "bi-x-circle-fill",
            Tone = approved ? "success" : "danger",
            Link = "/time-off"
        });
    }

    public void NotifyExpenseDecision(ExpenseClaim claim, string deciderEmail, ExpenseStatus status)
    {
        var submitter = _employees.GetAll().FirstOrDefault(e => e.FullName == claim.SubmittedBy);
        if (submitter is null) return;
        if (string.Equals(submitter.Email, deciderEmail, StringComparison.OrdinalIgnoreCase)) return; // actor == submitter

        var (verb, tone, icon) = status switch
        {
            ExpenseStatus.Approved => ("approved", "success", "bi-check-circle-fill"),
            ExpenseStatus.Rejected => ("rejected", "danger", "bi-x-circle-fill"),
            ExpenseStatus.Reimbursed => ("reimbursed", "primary", "bi-cash-stack"),
            _ => ("updated", "info", "bi-receipt")
        };

        Add(new NotificationItem
        {
            Recipient = submitter.Email,
            Title = $"Expense {verb}",
            Message = $"Your expense '{claim.Title}' ({claim.Currency} {claim.Amount:N0}) was {verb}.",
            Icon = icon,
            Tone = tone,
            Link = "/expenses"
        });
    }

    private string? ManagerEmailOf(Employee e) =>
        _employees.GetAll().FirstOrDefault(x => x.FullName == e.ManagerName)?.Email;

    private IEnumerable<string> EmailsInDepartment(string department) =>
        _employees.GetAll().Where(x => x.Department == department).Select(x => x.Email);

    // ---- Derive initial notifications per user + per role (option A) ----
    private void EnsureSeeded(string userEmail, IEnumerable<string> roles)
    {
        if (string.IsNullOrWhiteSpace(userEmail)) return;
        lock (_lock) { if (!_seeded.Add(userEmail)) return; }

        var roleSet = roles.ToHashSet();
        var emp = _employees.GetByEmail(userEmail);
        var now = DateTime.Now;
        var seed = new List<NotificationItem>
        {
            new() { Recipient = userEmail, Title = "Updated Travel Policy", Message = "Review the new travel policy effective 1 June.", Icon = "bi-megaphone-fill", Tone = "info", Link = "/announcements", CreatedAt = now.AddDays(-1) },
            new() { Recipient = userEmail, Title = "Q2 Town Hall", Message = "Company-wide town hall on 28 May at 10:00.", Icon = "bi-megaphone-fill", Tone = "primary", Link = "/announcements", CreatedAt = now.AddDays(-2) },
        };

        // ---- Personal (every user) ----
        if (emp is not null)
        {
            foreach (var d in _documents.GetForOwner(emp.FullName).Where(x => x.Status != DocumentStatus.Valid))
            {
                seed.Add(new()
                {
                    Recipient = userEmail,
                    Title = $"Document expiring: {d.Name}",
                    Message = $"Expires {d.ExpiryDate:dd MMM yyyy}. Tap to review your documents.",
                    Icon = "bi-folder-fill",
                    Tone = d.Status == DocumentStatus.Expired ? "danger" : "warning",
                    Link = "/documents",
                    CreatedAt = now.AddHours(-3)
                });
            }

            foreach (var t in _training.GetForEmployee(emp.FullName).Where(x => x.Status != TrainingStatus.Completed))
            {
                seed.Add(new()
                {
                    Recipient = userEmail,
                    Title = $"Training due: {t.CourseName}",
                    Message = $"Due {t.DueDate:dd MMM yyyy}. Tap to continue.",
                    Icon = "bi-mortarboard-fill",
                    Tone = "purple",
                    Link = "/training",
                    CreatedAt = now.AddHours(-5)
                });
            }
        }

        // ---- Manager: approvals for their direct reports ----
        if (roleSet.Contains(Roles.Manager) && emp is not null)
        {
            var reportIds = _employees.GetDirectReports(emp.FullName).Select(r => r.Id).ToHashSet();
            var pending = _timeOff.GetForEmployees(reportIds).Count(r => r.Status == TimeOffStatus.Pending);
            if (pending > 0)
                seed.Add(new() { Recipient = userEmail, Title = $"{pending} approval(s) awaiting you", Message = "Time-off requests from your team need a decision.", Icon = "bi-check2-square", Tone = "info", Link = "/approvals", CreatedAt = now.AddHours(-1) });
        }

        // ---- HR: org-wide approvals + compliance ----
        if (roleSet.Contains(Roles.HR))
        {
            var pendingAll = _timeOff.GetPending().Count();
            if (pendingAll > 0)
                seed.Add(new() { Recipient = userEmail, Title = $"{pendingAll} leave request(s) to review", Message = "Pending leave approvals across the company.", Icon = "bi-check2-square", Tone = "info", Link = "/approvals", CreatedAt = now.AddHours(-1) });

            if (_documents.VisaExpiringSoon > 0)
                seed.Add(new() { Recipient = userEmail, Title = $"{_documents.VisaExpiringSoon} visa(s) expiring soon", Message = "Review employee documents nearing expiry.", Icon = "bi-passport", Tone = "warning", Link = "/document-expiry", CreatedAt = now.AddHours(-2) });

            if (_documents.ContractsDue > 0)
                seed.Add(new() { Recipient = userEmail, Title = $"{_documents.ContractsDue} contract(s) due for renewal", Message = "Contracts approaching their end date.", Icon = "bi-file-earmark-text", Tone = "danger", Link = "/contracts", CreatedAt = now.AddHours(-4) });
        }

        // ---- Finance: expense review + payroll ----
        if (roleSet.Contains(Roles.Finance))
        {
            if (_expenses.PendingCount > 0)
                seed.Add(new() { Recipient = userEmail, Title = $"{_expenses.PendingCount} expense claim(s) to review", Message = "Claims awaiting finance approval.", Icon = "bi-receipt", Tone = "warning", Link = "/finance-dashboard", CreatedAt = now.AddHours(-1) });

            seed.Add(new() { Recipient = userEmail, Title = "Payroll cut-off in 5 days", Message = "Submit all payroll changes before the cut-off.", Icon = "bi-calendar2-week-fill", Tone = "danger", Link = "/finance-dashboard", CreatedAt = now.AddHours(-6) });
        }

        lock (_lock) _items.AddRange(seed);
    }
}
