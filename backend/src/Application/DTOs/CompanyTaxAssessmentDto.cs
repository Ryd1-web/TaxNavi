namespace TaxNavi.Application.DTOs;

public class CompanyTaxAssessmentDto
{
    public string Classification { get; set; } = string.Empty;
    public decimal Turnover { get; set; }
    public decimal FixedAssets { get; set; }
    public decimal AllowableExpenses { get; set; }
    public decimal AssessableProfit { get; set; }
    public decimal IncomeTaxRate { get; set; }
    public decimal IncomeTax { get; set; }
    public string LevyName { get; set; } = string.Empty;
    public decimal LevyRate { get; set; }
    public decimal Levy { get; set; }
    public string VerificationStatus { get; set; } = VerificationStatuses.PendingValidation;
}
