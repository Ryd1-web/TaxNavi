using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Services;

public interface ITaxRuleProvider
{
    int DefaultTaxYear { get; }
    IReadOnlyList<int> SupportedTaxYears { get; }
    MonetizationRules Monetization { get; }

    /// <summary>Returns the versioned rule set for a tax year, or null when the year is not supported.</summary>
    TaxRuleSet? FindRuleSet(int taxYear);
}
