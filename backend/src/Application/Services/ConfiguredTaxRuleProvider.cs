using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Services;

public class ConfiguredTaxRuleProvider : ITaxRuleProvider
{
    private readonly TaxRulesConfiguration _rules;

    public ConfiguredTaxRuleProvider(TaxRulesConfiguration rules)
    {
        _rules = rules;
    }

    public int DefaultTaxYear => _rules.DefaultTaxYear;

    public MonetizationRules Monetization => _rules.Monetization;

    public IReadOnlyList<int> SupportedTaxYears => _rules.RuleSets
        .SelectMany(set => Enumerable.Range(set.FirstTaxYear, (set.LastTaxYear ?? _rules.DefaultTaxYear) - set.FirstTaxYear + 1))
        .Where(year => year <= _rules.DefaultTaxYear)
        .Distinct()
        .OrderByDescending(year => year)
        .ToList();

    public TaxRuleSet? FindRuleSet(int taxYear)
    {
        return _rules.RuleSets.FirstOrDefault(set => set.AppliesTo(taxYear));
    }
}
