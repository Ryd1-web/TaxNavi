namespace TaxNavi.Application.DTOs;

public class TaxRulesPublicDto
{
    public int DefaultTaxYear { get; set; }
    public List<int> SupportedTaxYears { get; set; } = new();
    public RuleSetSummaryDto RuleSet { get; set; } = new();
    public int TaxYear { get; set; }
    public string ReliefModel { get; set; } = string.Empty;
    public decimal RentReliefRate { get; set; }
    public decimal RentReliefCap { get; set; }
    public List<TaxBandChargeDto> Bands { get; set; } = new();
    public VatRules Vat { get; set; } = new();
    public CompanyIncomeTaxRules CompanyIncomeTax { get; set; } = new();
    public List<string> Notes { get; set; } = new();
    public List<ComplianceDeadlineRule> Deadlines { get; set; } = new();
    public MonetizationRules Monetization { get; set; } = new();
}
