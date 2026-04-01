namespace TaxNavi.Application.Services;

using Domain.Interfaces;
using Domain.Entities;
using DTOs;
using System.Text.Json;

public class TaxCalculationService : ITaxCalculationService
{
    // Nigerian Tax Constants for 2024
    private const decimal CONSOLIDATED_RELIEF_FLAT = 200_000m;
    private const decimal CONSOLIDATED_RELIEF_PERCENTAGE = 0.01m; // 1% of gross income
    private const decimal CONSOLIDATED_RELIEF_CAP = 0.20m; // 20% of gross income

    // Tax bands
    private static readonly Dictionary<decimal, decimal> TaxBands = new()
    {
        { 300_000, 0.07m },      // 7%
        { 600_000, 0.11m },      // 11%
        { 1_000_000, 0.15m },    // 15%
        { 3_000_000, 0.19m },    // 19%
        { 6_000_000, 0.21m },    // 21%
        { decimal.MaxValue, 0.24m }  // 24%
    };

    public decimal CalculatePIT(decimal grossIncome, decimal allowableDeductions)
    {
        var taxableIncome = grossIncome - allowableDeductions;
        if (taxableIncome <= 0) return 0;

        return ApplyTaxBands(taxableIncome);
    }

    public decimal CalculateConsolidatedRelief(decimal grossIncome)
    {
        var flatRelief = CONSOLIDATED_RELIEF_FLAT;
        var percentageRelief = grossIncome * CONSOLIDATED_RELIEF_PERCENTAGE;
        var maxRelief = grossIncome * CONSOLIDATED_RELIEF_CAP;

        var relief = Math.Max(flatRelief, percentageRelief);
        return Math.Min(relief, maxRelief);
    }

    public decimal ApplyTaxBands(decimal taxableIncome)
    {
        decimal tax = 0;
        decimal previousLimit = 0;

        foreach (var band in TaxBands)
        {
            if (taxableIncome <= previousLimit)
                break;

            var bandRate = band.Value;
            var bandLimit = band.Key;
            var bandIncome = Math.Min(taxableIncome, bandLimit) - previousLimit;

            tax += bandIncome * bandRate;
            previousLimit = bandLimit;
        }

        return tax;
    }

    public async Task<TaxCalculationResult> CalculateIndividualTaxAsync(IndividualTaxInput input)
    {
        // Calculate total gross income
        var grossIncome = input.MonthlySalary * 12 + input.AnnualBonus + input.Allowances +
                         input.BusinessIncome + input.FreelanceIncome + input.InvestmentIncome;

        // Allowable deductions
        var allowableDeductions = input.RentPaid + input.OtherExpenses;

        // Calculate consolidated relief (choose the better option)
        var consolidatedRelief = CalculateConsolidatedRelief(grossIncome);

        // Tax is on (Gross Income - Allowable Deductions - Consolidated Relief)
        var taxableIncome = Math.Max(0, grossIncome - allowableDeductions - consolidatedRelief);

        var taxLiability = ApplyTaxBands(taxableIncome);

        // Projected savings information
        var result = new TaxCalculationResult
        {
            TaxLiability = taxLiability,
            AllowableDeductions = allowableDeductions,
            ConsolidatedRelief = consolidatedRelief,
            ProjectedSavings = CalculateProjectedSavings(grossIncome, taxLiability),
            TaxBands = CalculateTaxBandBreakdown(taxableIncome)
        };

        return await Task.FromResult(result);
    }

    public async Task<SMETaxCalculationResult> CalculateSMETaxAsync(SMETaxInput input)
    {
        // VAT Eligibility: Turnover > ₦25 million
        var vatEligible = input.AnnualTurnover > 25_000_000;
        var vatLiability = vatEligible ? input.VatableIncome * 0.075m : 0;

        // WHT Calculation (simplified, 5% on relevant expenditures)
        var whtLiability = input.OperatingExpenses * 0.05m;

        // CIT Calculation (simplified)
        var taxableProfit = input.AnnualTurnover - input.OperatingExpenses;
        var citLiability = taxableProfit > 0 ? taxableProfit * 0.30m : 0;

        var totalObligation = vatLiability + whtLiability + citLiability;

        var result = new SMETaxCalculationResult
        {
            VatEligible = vatEligible,
            VatLiability = vatLiability,
            WhtLiability = whtLiability,
            TotalTaxObligation = totalObligation,
            ComplianceRoadmap = GenerateComplianceRoadmap(input, vatEligible)
        };

        return await Task.FromResult(result);
    }

    private decimal CalculateProjectedSavings(decimal grossIncome, decimal taxLiability)
    {
        // Simplified savings projection based on tax optimization strategies
        var potentialOptimization = grossIncome * 0.05m; // 5% potential savings
        return Math.Min(potentialOptimization, taxLiability * 0.15m); // Up to 15% of tax liability
    }

    private Dictionary<string, decimal> CalculateTaxBandBreakdown(decimal taxableIncome)
    {
        var breakdown = new Dictionary<string, decimal>();
        decimal previousLimit = 0;
        int bandNumber = 1;

        foreach (var band in TaxBands)
        {
            if (taxableIncome <= previousLimit)
                break;

            var bandLimit = band.Key;
            var bandRate = band.Value;
            var bandIncome = Math.Min(taxableIncome, bandLimit) - previousLimit;
            var bandTax = bandIncome * bandRate;

            breakdown[$"Band {bandNumber} ({bandRate:P})"] = bandTax;
            previousLimit = bandLimit;
            bandNumber++;
        }

        return breakdown;
    }

    private string GenerateComplianceRoadmap(SMETaxInput input, bool vatEligible)
    {
        var roadmap = "SME Tax Compliance Roadmap:\n";
        roadmap += $"1. Annual Turnover: ₦{input.AnnualTurnover:N0}\n";
        roadmap += $"2. VAT Status: {(vatEligible ? "Registered (Turnover > ₦25M)" : "Not Registered")}\n";
        roadmap += "3. Key Obligations:\n";
        roadmap += "   - File Annual CIT Return Within 6 months of year-end\n";
        roadmap += $"   - {(vatEligible ? "Submit VAT Returns Monthly" : "Monitor turnover for VAT registration threshold")}\n";
        roadmap += "   - Maintain Proper Records & Documentation\n";
        roadmap += "   - Withholding Tax Compliance\n";

        return roadmap;
    }
}
