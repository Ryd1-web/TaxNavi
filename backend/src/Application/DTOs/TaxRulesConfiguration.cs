namespace TaxNavi.Application.DTOs;

public class TaxRulesConfiguration
{
    public int DefaultTaxYear { get; set; } = 2026;
    public List<TaxRuleSet> RuleSets { get; set; } = new();
    public MonetizationRules Monetization { get; set; } = new();
}
