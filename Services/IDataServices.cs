using HCP.HRPortal.Models;

namespace HCP.HRPortal.Services;

// ---------------------------------------------------------------------------
// Data-source seams (brief §8). Pages depend only on these interfaces, so the
// current EF/in-memory implementations (Fake*Service) can later be swapped for
// SharePoint / Microsoft Graph implementations by changing the DI registration
// in Program.cs — no page changes required.
// ---------------------------------------------------------------------------

public interface IEmployeeService
{
    IReadOnlyList<Employee> GetAll();
    Employee? GetById(int id);
    Employee? GetByEmail(string email);
    void SetPhoto(int employeeId, string photoUrl);
    IReadOnlyList<Employee> GetDirectReports(string managerName);
    int HeadCount { get; }
    int OnLeaveCount { get; }
    IEnumerable<IGrouping<string, Employee>> ByDepartment();
    IEnumerable<string> Departments();
    void Add(Employee employee);
    void Update(Employee employee);
}

public interface ITimeOffService
{
    IEnumerable<TimeOffRequest> GetAll();
    IEnumerable<TimeOffRequest> GetForEmployee(int employeeId);
    IEnumerable<TimeOffRequest> GetPending();
    IEnumerable<TimeOffRequest> GetForEmployees(IEnumerable<int> employeeIds);
    int PendingCount { get; }
    void Add(TimeOffRequest request);
    void SetStatus(int id, TimeOffStatus status);
}

public interface IExpenseService
{
    IEnumerable<ExpenseClaim> GetAll();
    IEnumerable<ExpenseClaim> GetForUser(string name);
    IEnumerable<ExpenseClaim> GetByStatus(ExpenseStatus status);
    decimal TotalSubmitted { get; }
    decimal TotalReimbursed { get; }
    int PendingCount { get; }
    void Add(ExpenseClaim claim);
    void SetStatus(int id, ExpenseStatus status);
}

public interface IDocumentService
{
    IEnumerable<DocumentRecord> GetAll();
    IEnumerable<DocumentRecord> GetForOwner(string owner);
    IEnumerable<DocumentRecord> GetByType(string type);
    IEnumerable<DocumentRecord> GetExpiring();
    int ExpiringSoonCount { get; }
    int ExpiredCount { get; }
    int VisaExpiringSoon { get; }
    int PassportExpiringSoon { get; }
    int ContractsDue { get; }
    IEnumerable<DocumentRecord> GetFinanceDocuments();
    void Add(DocumentRecord doc);
}

public interface ITrainingService
{
    IEnumerable<TrainingRecord> GetAll();
    IEnumerable<TrainingRecord> GetForEmployee(string name);
    int MandatoryOutstanding { get; }
    void SetProgress(int id, int percent);
}

public interface ICalendarService
{
    IEnumerable<CalendarEvent> GetAll();
    IEnumerable<CalendarEvent> GetUpcoming(int count = 5);
    IEnumerable<CalendarEvent> GetForMonth(int year, int month);
    IReadOnlyList<CalendarItem> GetItems(CalendarView view, Employee? me, IEnumerable<string>? teamNames = null);
}
