using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Services;

/// <summary>
/// Splits a computed report into the free tier and the premium tier. This class never
/// decides entitlement itself — callers pass the result of a server-side entitlement check.
/// </summary>
public class TaxAccessControlService : ITaxAccessControlService
{
    private readonly ITaxRuleProvider _ruleProvider;

    public TaxAccessControlService(ITaxRuleProvider ruleProvider)
    {
        _ruleProvider = ruleProvider;
    }

    public FreeTaxSummaryDto GetFreeTaxSummary(FullTaxReportDto report)
    {
        var tipCount = report.OptimizationTips.Count;
        var flagCount = report.ComplianceWarnings.Count;

        // Teasers describe what is in the premium report without revealing its content.
        var teasers = new List<string>();
        if (tipCount > 0)
        {
            teasers.Add($"{tipCount} personalised optimisation {(tipCount == 1 ? "opportunity" : "opportunities")} identified.");
        }
        if (flagCount > 0)
        {
            teasers.Add($"{flagCount} compliance {(flagCount == 1 ? "point" : "points")} to review before you file.");
        }
        if (report.TaxBandBreakdown.Count > 0)
        {
            teasers.Add($"Your income spans {report.TaxBandBreakdown.Count} tax {(report.TaxBandBreakdown.Count == 1 ? "band" : "bands")} — see exactly how each naira is taxed.");
        }

        return new FreeTaxSummaryDto
        {
            TaxYear = report.TaxYear,
            TaxType = report.TaxType,
            TaxpayerType = report.TaxpayerType,
            EstimatedTaxPayable = report.TaxPayable,
            GrossIncome = report.GrossIncome,
            TotalDeductionsAndReliefs = report.TotalDeductionsAndReliefs,
            BasicTaxableIncome = report.TaxableIncome,
            EffectiveTaxRate = report.EffectiveTaxRate,
            EstimatedMonthlyPaye = report.EstimatedMonthlyPaye,
            CompanyClassification = report.CompanyTax?.Classification,
            OptimisationOpportunityCount = tipCount,
            ComplianceFlagCount = flagCount,
            TeaserInsights = teasers,
            NextSteps = report.NextSteps.Take(3).ToList(),
            Vat = report.Vat,
            ApplicableRegime = report.ApplicableRegime,
            RuleSet = report.RuleSet,
            Assumptions = report.Assumptions
        };
    }

    public TaxReportAccessResponseDto BuildResponse(FullTaxReportDto report, string? unlockedVia, bool paymentsEnabled, Guid? reportId = null)
    {
        var granted = !string.IsNullOrEmpty(unlockedVia);
        var response = new TaxReportAccessResponseDto
        {
            ReportId = reportId,
            AccessGranted = granted,
            AccessLevel = granted ? "Full" : "Free",
            UnlockedVia = unlockedVia,
            FreeSummary = GetFreeTaxSummary(report),
            PremiumGate = BuildPremiumGate(report.TaxpayerType, paymentsEnabled)
        };

        if (granted)
        {
            report.SavedReportId = reportId;
            report.CanDownloadPdf = reportId.HasValue;
            report.RequiresAuthenticationToDownloadPdf = !reportId.HasValue;
            response.FullReport = report;
        }

        return response;
    }

    private PremiumGateDto BuildPremiumGate(string reportType, bool paymentsEnabled)
    {
        var pricing = _ruleProvider.Monetization;
        return new PremiumGateDto
        {
            Headline = "Get your complete tax report",
            Message = "See exactly how your tax was calculated, which deductions you are missing, and what to do before you file.",
            PaymentAmount = pricing.PriceFor(reportType),
            SubscriptionAmount = pricing.SmeSubscriptionPrice,
            Currency = pricing.Currency,
            LockedFeatures = new List<string>
            {
                "Band-by-band tax breakdown",
                "Deduction analysis with what you can still claim",
                "Personalised optimisation recommendations",
                "Compliance checks and filing obligations",
                "Downloadable PDF report"
            },
            RequiresAccount = false,
            PaymentsEnabled = paymentsEnabled
        };
    }
}
