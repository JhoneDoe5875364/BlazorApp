namespace HCP.HRPortal.Models;

public enum CertificateStatus { Pending, Processing, Issued, Rejected }

/// <summary>An employee's request for a formal salary certificate (processed by HR/Finance).</summary>
public class SalaryCertificateRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string Purpose { get; set; } = "";      // Bank loan, Visa application, Rental agreement…
    public string Addressee { get; set; } = "";    // who the certificate is addressed to
    public DateOnly RequestedOn { get; set; }
    public CertificateStatus Status { get; set; } = CertificateStatus.Pending;
}
