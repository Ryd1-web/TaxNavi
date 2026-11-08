namespace TaxNavi.Application.DTOs;

public class TaxRuleSet
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int FirstTaxYear { get; set; }
    public int? LastTaxYear { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string LegalBasis { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = VerificationStatuses.PendingValidation;
    public PersonalIncomeTaxRules PersonalIncomeTax { get; set; } = new();
    public CompanyIncomeTaxRules CompanyIncomeTax { get; set; } = new();
    public VatRules Vat { get; set; } = new();
    public List<string> Notes { get; set; } = new();
    public List<TaxSourceDto> Sources { get; set; } = new();
    public List<ComplianceDeadlineRule> Deadlines { get; set; } = new();

    public bool AppliesTo(int taxYear)
    {
        return taxYear >= FirstTaxYear && (LastTaxYear == null || taxYear <= LastTaxYear);
    }
}
