using System.ComponentModel.DataAnnotations.Schema;

namespace HCP.HRPortal.Models;

public enum DocumentStatus
{
    Valid,
    ExpiringSoon,
    Expired
}

public class DocumentRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Owner { get; set; } = "";
    public DateOnly IssueDate { get; set; }
    public DateOnly ExpiryDate { get; set; }

    [NotMapped]
    public int DaysUntilExpiry =>
        ExpiryDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;

    [NotMapped]
    public DocumentStatus Status => DaysUntilExpiry switch
    {
        < 0 => DocumentStatus.Expired,
        <= 60 => DocumentStatus.ExpiringSoon,
        _ => DocumentStatus.Valid
    };
}
