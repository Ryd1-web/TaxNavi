namespace TaxNavi.Application.DTOs;

public class VatAssessmentDto
{
    public bool Applicable { get; set; }
    public decimal AnnualTurnover { get; set; }
    public decimal Threshold { get; set; }
    public decimal VatRate { get; set; }
    public bool VatRequired { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = VerificationStatuses.PendingValidation;
}
