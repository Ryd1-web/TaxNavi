namespace TaxNavi.Application.DTOs;

public class FreeTaxSummaryDto
{
    public int TaxYear { get; set; }
    public string TaxType { get; set; } = TaxTypes.PersonalIncomeTax;
    public string TaxpayerType { get; set; } = TaxpayerKinds.Individual;
    public decimal EstimatedTaxPayable { get; set; }
    public decimal GrossIncome { get; set; }
    public decimal TotalDeductionsAndReliefs { get; set; }
    public decimal BasicTaxableIncome { get; set; }
    public decimal EffectiveTaxRate { get; set; }
    public decimal? EstimatedMonthlyPaye { get; set; }
    public string? CompanyClassification { get; set; }
    public int OptimisationOpportunityCount { get; set; }
    public int ComplianceFlagCount { get; set; }
    public List<string> TeaserInsights { get; set; } = new();
    public List<string> NextSteps { get; set; } = new();
    public VatAssessmentDto Vat { get; set; } = new();
    public string ApplicableRegime { get; set; } = string.Empty;
    public RuleSetSummaryDto RuleSet { get; set; } = new();
    public List<string> Assumptions { get; set; } = new();
}
