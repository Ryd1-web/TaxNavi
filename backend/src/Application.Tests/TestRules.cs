using Microsoft.Extensions.Configuration;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;

namespace TaxNavi.Application.Tests;

/// <summary>Loads the rule configuration shipped in Api/appsettings.json so tests verify the real config.</summary>
public static class TestRules
{
    private static readonly Lazy<TaxRulesConfiguration> Config = new(() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build()
            .GetSection("TaxRules")
            .Get<TaxRulesConfiguration>()!);

    public static ITaxRuleProvider Provider => new ConfiguredTaxRuleProvider(Config.Value);

    public static NigerianTaxCalculationEngine Engine => new(Provider);

    public static TaxCalculationRequestDto Salary(decimal annual, int taxYear) => new()
    {
        TaxYear = taxYear,
        TaxpayerType = TaxpayerKinds.Individual,
        IncomeSources = new List<IncomeStreamDto>
        {
            new() { Name = "Salary", Category = IncomeCategories.Employment, AnnualAmount = annual }
        }
    };
}
