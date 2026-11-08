namespace TaxNavi.Application.DTOs;

public class FullTaxReportDto
{
    public int TaxYear { get; set; }
    public string TaxType { get; set; } = TaxTypes.PersonalIncomeTax;
    public string TaxpayerType { get; set; } = TaxpayerKinds.Individual;
    public string BusinessType { get; set; } = string.Empty;
    public string BusinessStructure { get; set; } = string.Empty;
    public decimal GrossIncome { get; set; }
    public decimal AdjustedGrossIncome { get; set; }
    public decimal EmploymentIncome { get; set; }
    public decimal SelfEmployedIncome { get; set; }
    public decimal SelfEmployedExpenses { get; set; }
    public decimal InvestmentIncome { get; set; }
    public decimal OtherIncome { get; set; }
    public decimal IncomeExcludedAsFinalTax { get; set; }
    public decimal ConsolidatedReliefAllowance { get; set; }
    public decimal RentRelief { get; set; }
    public AllowableDeductionInputDto Deductions { get; set; } = new();
    public List<DeductionLineDto> DeductionAnalysis { get; set; } = new();
    public decimal TotalAllowableDeductions { get; set; }
    public decimal TotalDeductionsAndReliefs { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal TaxPayable { get; set; }
    public decimal EffectiveTaxRate { get; set; }
    public decimal? EstimatedMonthlyPaye { get; set; }
    public CompanyTaxAssessmentDto? CompanyTax { get; set; }
    public VatAssessmentDto Vat { get; set; } = new();
    public List<TaxBandChargeDto> TaxBandBreakdown { get; set; } = new();
    public List<CalculationStepDto> CalculationSteps { get; set; } = new();
    public List<string> OptimizationTips { get; set; } = new();
    public List<string> ComplianceWarnings { get; set; } = new();
    public List<string> NextSteps { get; set; } = new();
    public List<string> Assumptions { get; set; } = new();
    public RuleSetSummaryDto RuleSet { get; set; } = new();
    public Guid? SavedReportId { get; set; }
    public bool CanDownloadPdf { get; set; }
    public bool RequiresAuthenticationToDownloadPdf { get; set; } = true;
    public string ApplicableRegime { get; set; } = string.Empty;
}
