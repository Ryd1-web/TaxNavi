namespace TaxNavi.Application.DTOs;

public class VatRules
{
    public decimal RegistrationThreshold { get; set; }
    public decimal Rate { get; set; }

    /// <summary>True when turnover exactly at the threshold is still exempt ("not exceeding").</summary>
    public bool ExemptAtThreshold { get; set; }
    public string VerificationStatus { get; set; } = VerificationStatuses.PendingValidation;
    public string Note { get; set; } = string.Empty;
}
