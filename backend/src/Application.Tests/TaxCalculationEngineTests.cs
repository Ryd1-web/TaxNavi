using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using Xunit;

namespace TaxNavi.Application.Tests;

/// <summary>Regression tests for the pre-2026 PITA rule set (tax years 2024–2025).</summary>
public class LegacyPitaEngineTests
{
    private readonly NigerianTaxCalculationEngine _engine = TestRules.Engine;

    [Fact]
    public void LowIncomeEarner_UsesCraAndFirstBandCorrectly()
    {
        var report = _engine.GenerateFullReport(TestRules.Salary(300_000m, 2025));

        Assert.Equal("pita-legacy", report.RuleSet.Id);
        Assert.Equal(260_000m, report.ConsolidatedReliefAllowance);
        Assert.Equal(40_000m, report.TaxableIncome);
        Assert.Equal(2_800m, report.TaxPayable);
    }

    [Fact]
    public void MidIncomeEarner_AppliesBandsAndAllowableDeductionsCorrectly()
    {
        var request = TestRules.Salary(2_400_000m, 2025);
        request.AllowableDeductions = new AllowableDeductionInputDto
        {
            PensionContributions = 120_000m,
            NhfContributions = 30_000m,
            NhisContributions = 20_000m,
            LifeAssurancePremium = 10_000m
        };

        var report = _engine.GenerateFullReport(request);

        Assert.Equal(680_000m, report.ConsolidatedReliefAllowance);
        Assert.Equal(180_000m, report.TotalAllowableDeductions);
        Assert.Equal(1_540_000m, report.TaxableIncome);
        Assert.Equal(212_600m, report.TaxPayable);
    }

    [Fact]
    public void HighIncomeEarner_UsesTopBandCorrectly()
    {
        var request = TestRules.Salary(6_000_000m, 2025);
        request.AllowableDeductions.PensionContributions = 300_000m;

        var report = _engine.GenerateFullReport(request);

        Assert.Equal(1_400_000m, report.ConsolidatedReliefAllowance);
        Assert.Equal(4_300_000m, report.TaxableIncome);
        Assert.Equal(824_000m, report.TaxPayable);
        Assert.Equal(0.24m, report.TaxBandBreakdown[^1].Rate);
    }

    [Fact]
    public void Freelancer_CraIsComputedOnProfitNotReceipts()
    {
        var report = _engine.GenerateFullReport(new TaxCalculationRequestDto
        {
            TaxYear = 2025,
            TaxpayerType = TaxpayerKinds.SelfEmployed,
            IncomeSources = new List<IncomeStreamDto>
            {
                new() { Name = "Consulting", Category = IncomeCategories.Business, AnnualAmount = 4_000_000m },
                new() { Name = "Freelance gigs", Category = IncomeCategories.Freelance, AnnualAmount = 1_200_000m }
            },
            Expenses = new List<ExpenseItemDto>
            {
                new() { Name = "Operating costs", Category = ExpenseCategories.Business, AnnualAmount = 1_500_000m }
            },
            AllowableDeductions = new AllowableDeductionInputDto { PensionContributions = 100_000m }
        });

        Assert.Equal(5_200_000m, report.GrossIncome);
        Assert.Equal(3_700_000m, report.AdjustedGrossIncome);
        Assert.Equal(940_000m, report.ConsolidatedReliefAllowance);
        Assert.Equal(2_660_000m, report.TaxableIncome);
        Assert.Equal(446_600m, report.TaxPayable);
    }

    [Fact]
    public void RentIsNotDeductibleUnderCra()
    {
        var request = TestRules.Salary(2_400_000m, 2025);
        request.AllowableDeductions.AnnualRentPaid = 1_000_000m;

        var report = _engine.GenerateFullReport(request);

        Assert.Equal(0m, report.RentRelief);
        Assert.Contains(report.DeductionAnalysis, line => line.Label == "Rent paid" && line.Allowed == 0m);
    }

    [Theory]
    [InlineData(24_999_999, false)]
    [InlineData(25_000_000, true)]
    public void VatThreshold_EdgeCases(decimal turnover, bool expectedRequired)
    {
        var report = _engine.GenerateFullReport(new TaxCalculationRequestDto
        {
            TaxYear = 2025,
            TaxpayerType = TaxpayerKinds.SME,
            BusinessStructure = BusinessStructures.SoleProprietor,
            IncomeSources = new List<IncomeStreamDto> { new() { Name = "Sales", Category = IncomeCategories.Business, AnnualAmount = turnover } },
            Vat = new VatCheckInputDto { AnnualTurnover = turnover }
        });

        Assert.Equal(expectedRequired, report.Vat.VatRequired);
    }
}

/// <summary>Nigeria Tax Act 2025 rule set (tax years from 2026).</summary>
public class Nta2025EngineTests
{
    private readonly NigerianTaxCalculationEngine _engine = TestRules.Engine;

    [Theory]
    [InlineData(800_000, 0)]
    [InlineData(3_000_000, 330_000)]
    [InlineData(12_000_000, 1_950_000)]
    [InlineData(25_000_000, 4_680_000)]
    [InlineData(50_000_000, 10_430_000)]
    [InlineData(60_000_000, 12_930_000)]
    public void Bands_MatchPublishedCumulativeFigures(decimal taxable, decimal expectedTax)
    {
        var report = _engine.GenerateFullReport(TestRules.Salary(taxable, 2026));

        Assert.Equal("nta-2025", report.RuleSet.Id);
        Assert.Equal(0m, report.ConsolidatedReliefAllowance);
        Assert.Equal(expectedTax, report.TaxPayable);
    }

    [Fact]
    public void DefaultsToConfiguredTaxYear()
    {
        var request = TestRules.Salary(3_000_000m, 2026);
        request.TaxYear = null;

        var report = _engine.GenerateFullReport(request);

        Assert.Equal(2026, report.TaxYear);
        Assert.Equal(27_500m, report.EstimatedMonthlyPaye);
    }

    [Fact]
    public void RentReliefAndStatutoryDeductions_ReduceTaxableIncome()
    {
        var request = TestRules.Salary(5_000_000m, 2026);
        request.AllowableDeductions = new AllowableDeductionInputDto { AnnualRentPaid = 1_200_000m, PensionContributions = 400_000m };

        var report = _engine.GenerateFullReport(request);

        Assert.Equal(240_000m, report.RentRelief);
        Assert.Equal(640_000m, report.TotalDeductionsAndReliefs);
        Assert.Equal(4_360_000m, report.TaxableIncome);
        Assert.Equal(574_800m, report.TaxPayable);
    }

    [Fact]
    public void RentRelief_IsCapped()
    {
        var request = TestRules.Salary(30_000_000m, 2026);
        request.AllowableDeductions.AnnualRentPaid = 5_000_000m;

        var report = _engine.GenerateFullReport(request);

        Assert.Equal(500_000m, report.RentRelief);
    }

    [Fact]
    public void IncomeTaxedAtSourceAsFinalTax_IsExcluded()
    {
        var request = TestRules.Salary(3_000_000m, 2026);
        request.IncomeSources.Add(new IncomeStreamDto { Name = "Dividends", Category = IncomeCategories.Investment, AnnualAmount = 1_000_000m, FinalTaxDeducted = true });

        var report = _engine.GenerateFullReport(request);

        Assert.Equal(4_000_000m, report.GrossIncome);
        Assert.Equal(1_000_000m, report.IncomeExcludedAsFinalTax);
        Assert.Equal(330_000m, report.TaxPayable);
    }

    [Fact]
    public void Report_DisclosesAssumptionsSourcesAndNextSteps()
    {
        var report = _engine.GenerateFullReport(TestRules.Salary(3_000_000m, 2026));

        Assert.Contains(report.Assumptions, a => a.Contains("Tax year 2026"));
        Assert.NotEmpty(report.RuleSet.Sources);
        Assert.Contains(report.NextSteps, s => s.StartsWith("Personal income tax return"));
    }

    [Theory]
    [InlineData(100_000_000, false)]
    [InlineData(100_000_001, true)]
    public void Vat_SmallBusinessThresholdIsInclusive(decimal turnover, bool expectedRequired)
    {
        var report = _engine.GenerateFullReport(new TaxCalculationRequestDto
        {
            TaxYear = 2026,
            TaxpayerType = TaxpayerKinds.SME,
            BusinessStructure = BusinessStructures.SoleProprietor,
            IncomeSources = new List<IncomeStreamDto> { new() { Name = "Sales", Category = IncomeCategories.Business, AnnualAmount = turnover } },
            Vat = new VatCheckInputDto { AnnualTurnover = turnover }
        });

        Assert.Equal(expectedRequired, report.Vat.VatRequired);
        Assert.Equal(VerificationStatuses.PendingValidation, report.Vat.VerificationStatus);
    }

    [Fact]
    public void Vat_NotApplicableToSalaryOnly()
    {
        var report = _engine.GenerateFullReport(TestRules.Salary(3_000_000m, 2026));
        Assert.False(report.Vat.Applicable);
        Assert.False(report.Vat.VatRequired);
    }

    [Fact]
    public void UnsupportedTaxYear_Throws()
    {
        Assert.Throws<UnsupportedTaxYearException>(() => _engine.GenerateFullReport(TestRules.Salary(1_000_000m, 2019)));
    }
}

public class CompanyIncomeTaxTests
{
    private readonly NigerianTaxCalculationEngine _engine = TestRules.Engine;

    private static TaxCalculationRequestDto Company(decimal turnover, decimal expenses, decimal assets, bool professional = false, int year = 2026) => new()
    {
        TaxYear = year,
        TaxpayerType = TaxpayerKinds.SME,
        BusinessStructure = BusinessStructures.LimitedCompany,
        BusinessType = "Retail",
        FixedAssets = assets,
        IsProfessionalServices = professional,
        IncomeSources = new List<IncomeStreamDto> { new() { Name = "Sales", Category = IncomeCategories.Business, AnnualAmount = turnover } },
        Expenses = new List<ExpenseItemDto> { new() { Name = "Operating", Category = ExpenseCategories.Operating, AnnualAmount = expenses } },
        Vat = new VatCheckInputDto { AnnualTurnover = turnover }
    };

    [Fact]
    public void SmallCompany_PaysZeroCitAndNoLevy()
    {
        var report = _engine.GenerateFullReport(Company(80_000_000m, 30_000_000m, 50_000_000m));

        Assert.Equal(TaxTypes.CompanyIncomeTax, report.TaxType);
        Assert.Equal("Small company", report.CompanyTax!.Classification);
        Assert.Equal(50_000_000m, report.CompanyTax.AssessableProfit);
        Assert.Equal(0m, report.TaxPayable);
    }

    [Fact]
    public void ProfessionalServicesCompany_IsNotSmall()
    {
        var report = _engine.GenerateFullReport(Company(80_000_000m, 30_000_000m, 50_000_000m, professional: true));

        Assert.Equal("Other company", report.CompanyTax!.Classification);
        Assert.Equal(15_000_000m, report.CompanyTax.IncomeTax);
        Assert.Equal(2_000_000m, report.CompanyTax.Levy);
        Assert.Equal(17_000_000m, report.TaxPayable);
    }

    [Fact]
    public void CompanyAboveAssetLimit_IsNotSmall()
    {
        var report = _engine.GenerateFullReport(Company(80_000_000m, 30_000_000m, 300_000_000m));
        Assert.Equal("Other company", report.CompanyTax!.Classification);
    }

    [Fact]
    public void LargerCompany_Pays30PercentPlusDevelopmentLevy()
    {
        var report = _engine.GenerateFullReport(Company(300_000_000m, 200_000_000m, 100_000_000m));

        Assert.Equal(30_000_000m, report.CompanyTax!.IncomeTax);
        Assert.Equal(4_000_000m, report.CompanyTax.Levy);
        Assert.Equal(34_000_000m, report.TaxPayable);
        Assert.True(report.Vat.VatRequired);
    }

    [Fact]
    public void LegacyMediumCompany_Pays20PercentPlusTertiaryEducationTax()
    {
        var report = _engine.GenerateFullReport(Company(60_000_000m, 20_000_000m, 0m, year: 2025));

        Assert.Equal("Medium company", report.CompanyTax!.Classification);
        Assert.Equal(8_000_000m, report.CompanyTax.IncomeTax);
        Assert.Equal(1_200_000m, report.CompanyTax.Levy);
    }
}

public class AccessControlTests
{
    private readonly NigerianTaxCalculationEngine _engine = TestRules.Engine;
    private readonly TaxAccessControlService _access = new(TestRules.Provider);

    [Fact]
    public void FreeTier_NeverContainsPremiumContent()
    {
        var report = _engine.GenerateFullReport(TestRules.Salary(5_000_000m, 2026));
        var tips = report.OptimizationTips.ToList();

        var response = _access.BuildResponse(report, unlockedVia: null, paymentsEnabled: false);

        Assert.False(response.AccessGranted);
        Assert.Null(response.FullReport);
        Assert.Equal(report.TaxPayable, response.FreeSummary.EstimatedTaxPayable);
        Assert.Equal(tips.Count, response.FreeSummary.OptimisationOpportunityCount);
        Assert.DoesNotContain(response.FreeSummary.TeaserInsights, teaser => tips.Contains(teaser));
    }

    [Fact]
    public void EntitledResponse_IncludesFullReport()
    {
        var report = _engine.GenerateFullReport(TestRules.Salary(5_000_000m, 2026));
        var id = Guid.NewGuid();

        var response = _access.BuildResponse(report, UnlockSources.Purchase, paymentsEnabled: true, id);

        Assert.True(response.AccessGranted);
        Assert.NotNull(response.FullReport);
        Assert.True(response.FullReport!.CanDownloadPdf);
        Assert.Equal(id, response.ReportId);
    }
}
