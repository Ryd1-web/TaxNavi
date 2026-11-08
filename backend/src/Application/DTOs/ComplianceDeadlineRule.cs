namespace TaxNavi.Application.DTOs;

public class ComplianceDeadlineRule
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>"Monthly" (DayOfMonth of the following month) or "Annual" (Month/DayOfMonth).</summary>
    public string Frequency { get; set; } = "Monthly";
    public int DayOfMonth { get; set; }
    public int? Month { get; set; }

    /// <summary>Audiences: Employee, Employer, SelfEmployed, Company, VatRegistered.</summary>
    public List<string> AppliesTo { get; set; } = new();
    public string Authority { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = VerificationStatuses.PendingValidation;
}
