using System.Globalization;
using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Services;

/// <summary>
/// Deterministic Nigerian tax engine. Every rate, threshold and relief comes from the
/// versioned rule set for the requested tax year; nothing here is model-generated.
/// </summary>
public class NigerianTaxCalculationEngine : ITaxCalculationEngine
{
    private static readonly CultureInfo Naira = TaxFormat.Culture;
    private readonly ITaxRuleProvider _ruleProvider;

    public NigerianTaxCalculationEngine(ITaxRuleProvider ruleProvider)
    {
        _ruleProvider = ruleProvider;
    }

    public FullTaxReportDto GenerateFullReport(TaxCalculationRequestDto request)
    {
        var taxYear = request.TaxYear ?? _ruleProvider.DefaultTaxYear;
        var rules = _ruleProvider.FindRuleSet(taxYear)
            ?? throw new UnsupportedTaxYearException(taxYear, _ruleProvider.SupportedTaxYears);

        var incomes = request.IncomeSources
            .Select(source => new IncomeStreamDto
            {
                Name = source.Name,
                Category = source.Category,
                AnnualAmount = SanitizeMoney(source.AnnualAmount),
                FinalTaxDeducted = source.FinalTaxDeducted
            })
            .ToList();

        var expenses = request.Expenses
            .Select(expense => new ExpenseItemDto
            {
                Name = expense.Name,
                Category = expense.Category,
                AnnualAmount = SanitizeMoney(expense.AnnualAmount)
            })
            .ToList();

        var deductions = new AllowableDeductionInputDto
        {
            PensionContributions = SanitizeMoney(request.AllowableDeductions.PensionContributions),
            NhfContributions = SanitizeMoney(request.AllowableDeductions.NhfContributions),
            NhisContributions = SanitizeMoney(request.AllowableDeductions.NhisContributions),
            LifeAssurancePremium = SanitizeMoney(request.AllowableDeductions.LifeAssurancePremium),
            MortgageInterest = SanitizeMoney(request.AllowableDeductions.MortgageInterest),
            AnnualRentPaid = SanitizeMoney(request.AllowableDeductions.AnnualRentPaid)
        };

        var isCompany = string.Equals(request.TaxpayerType, TaxpayerKinds.SME, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(request.BusinessStructure, BusinessStructures.LimitedCompany, StringComparison.OrdinalIgnoreCase);

        var report = isCompany
            ? BuildCompanyReport(request, rules, incomes, expenses, deductions)
            : BuildPersonalReport(request, rules, incomes, expenses, deductions);

        report.TaxYear = taxYear;
        report.RuleSet = ToSummary(rules);
        report.TaxpayerType = string.IsNullOrWhiteSpace(request.TaxpayerType) ? TaxpayerKinds.Individual : request.TaxpayerType;
        report.BusinessType = request.BusinessType?.Trim() ?? string.Empty;
        report.BusinessStructure = request.BusinessStructure ?? string.Empty;
        report.NextSteps = BuildNextSteps(report, rules, expenses);
        report.Assumptions.InsertRange(0, new[]
        {
            $"Tax year {taxYear}, assessed under {rules.Name} (effective {rules.EffectiveFrom.ToString("d MMMM yyyy", Naira)}).",
            "Figures are annual estimates based only on the information you entered."
        });
        report.Assumptions.AddRange(rules.Notes);
        report.Assumptions = report.Assumptions.Distinct().ToList();

        return report;
    }

    public static RuleSetSummaryDto ToSummary(TaxRuleSet rules) => new()
    {
        Id = rules.Id,
        Name = rules.Name,
        EffectiveFrom = rules.EffectiveFrom,
        EffectiveTo = rules.EffectiveTo,
        LegalBasis = rules.LegalBasis,
        VerificationStatus = rules.VerificationStatus,
        Sources = rules.Sources
    };

    // -----------------------------------------------------------------------
    // Personal income tax (employees, self-employed, sole proprietors)
    // -----------------------------------------------------------------------

    private static FullTaxReportDto BuildPersonalReport(
        TaxCalculationRequestDto request,
        TaxRuleSet rules,
        List<IncomeStreamDto> incomes,
        List<ExpenseItemDto> expenses,
        AllowableDeductionInputDto deductions)
    {
        var pit = rules.PersonalIncomeTax;
        var chargeable = incomes.Where(source => !source.FinalTaxDeducted).ToList();

        var grossIncome = incomes.Sum(source => source.AnnualAmount);
        var excludedAsFinal = incomes.Where(source => source.FinalTaxDeducted).Sum(source => source.AnnualAmount);
        var employmentIncome = chargeable.Where(s => IsEmployment(s.Category)).Sum(s => s.AnnualAmount);
        var selfEmployedIncome = chargeable.Where(s => IsSelfEmployed(s.Category)).Sum(s => s.AnnualAmount);
        var investmentIncome = chargeable.Where(s => Is(s.Category, IncomeCategories.Investment)).Sum(s => s.AnnualAmount);
        var otherIncome = chargeable
            .Where(s => !IsEmployment(s.Category) && !IsSelfEmployed(s.Category) && !Is(s.Category, IncomeCategories.Investment))
            .Sum(s => s.AnnualAmount);

        var selfEmployedExpenses = selfEmployedIncome > 0m
            ? expenses.Where(e => IsBusinessExpense(e.Category)).Sum(e => e.AnnualAmount)
            : 0m;
        var netSelfEmployed = Math.Max(0m, selfEmployedIncome - selfEmployedExpenses);
        var adjustedGrossIncome = employmentIncome + netSelfEmployed + investmentIncome + otherIncome;

        var reliefs = CalculatePersonalReliefs(adjustedGrossIncome, deductions, pit);
        var taxableIncome = Math.Max(0m, adjustedGrossIncome - reliefs.TotalReliefsAndDeductions);
        var bands = CalculateBandBreakdown(taxableIncome, pit.Bands);
        var taxPayable = RoundCurrency(bands.Sum(b => b.TaxAmount));

        decimal? monthlyPaye = null;
        if (employmentIncome > 0m)
        {
            var employmentReliefs = CalculatePersonalReliefs(employmentIncome, deductions, pit);
            var employmentTaxable = Math.Max(0m, employmentIncome - employmentReliefs.TotalReliefsAndDeductions);
            monthlyPaye = RoundCurrency(CalculateBandBreakdown(employmentTaxable, pit.Bands).Sum(b => b.TaxAmount) / 12m);
        }

        var hasBusinessActivity = selfEmployedIncome > 0m ||
                                  string.Equals(request.TaxpayerType, TaxpayerKinds.SME, StringComparison.OrdinalIgnoreCase);
        var turnover = request.Vat.AnnualTurnover > 0m ? SanitizeMoney(request.Vat.AnnualTurnover) : selfEmployedIncome;

        var report = new FullTaxReportDto
        {
            TaxType = TaxTypes.PersonalIncomeTax,
            GrossIncome = RoundCurrency(grossIncome),
            AdjustedGrossIncome = RoundCurrency(adjustedGrossIncome),
            EmploymentIncome = RoundCurrency(employmentIncome),
            SelfEmployedIncome = RoundCurrency(selfEmployedIncome),
            SelfEmployedExpenses = RoundCurrency(selfEmployedExpenses),
            InvestmentIncome = RoundCurrency(investmentIncome),
            OtherIncome = RoundCurrency(otherIncome),
            IncomeExcludedAsFinalTax = RoundCurrency(excludedAsFinal),
            ConsolidatedReliefAllowance = reliefs.Cra,
            RentRelief = reliefs.RentRelief,
            Deductions = deductions,
            DeductionAnalysis = reliefs.Lines,
            TotalAllowableDeductions = reliefs.StatutoryDeductions,
            TotalDeductionsAndReliefs = reliefs.TotalReliefsAndDeductions,
            TaxableIncome = RoundCurrency(taxableIncome),
            TaxPayable = taxPayable,
            EffectiveTaxRate = grossIncome <= 0m ? 0m : Math.Round(taxPayable / grossIncome, 4, MidpointRounding.AwayFromZero),
            EstimatedMonthlyPaye = monthlyPaye,
            Vat = BuildVatAssessment(hasBusinessActivity, turnover, rules.Vat),
            TaxBandBreakdown = bands,
            ApplicableRegime = pit.RegimeName
        };

        report.CalculationSteps = BuildPersonalSteps(report, pit);
        report.OptimizationTips = BuildPersonalTips(report, deductions, pit);
        report.ComplianceWarnings = BuildPersonalWarnings(report, incomes, request);
        report.Assumptions.AddRange(BuildPersonalAssumptions(report, pit));
        return report;
    }

    private sealed record PersonalReliefs(
        decimal Cra,
        decimal RentRelief,
        decimal StatutoryDeductions,
        decimal TotalReliefsAndDeductions,
        List<DeductionLineDto> Lines);

    private static PersonalReliefs CalculatePersonalReliefs(
        decimal incomeBase,
        AllowableDeductionInputDto deductions,
        PersonalIncomeTaxRules pit)
    {
        var lines = new List<DeductionLineDto>();
        var usesCra = string.Equals(pit.ReliefModel, ReliefModels.Cra, StringComparison.OrdinalIgnoreCase);

        void Add(string label, decimal claimed, decimal allowed, string note)
        {
            if (claimed > 0m || allowed > 0m)
            {
                lines.Add(new DeductionLineDto { Label = label, Claimed = RoundCurrency(claimed), Allowed = RoundCurrency(allowed), Note = note });
            }
        }

        Add("Pension contributions", deductions.PensionContributions, deductions.PensionContributions, "Contributions under the Pension Reform Act are deductible.");
        Add("National Housing Fund (NHF)", deductions.NhfContributions, deductions.NhfContributions, "NHF contributions are deductible.");
        Add("Health insurance (NHIS)", deductions.NhisContributions, deductions.NhisContributions, "National health insurance contributions are deductible.");
        Add("Life assurance / annuity premium", deductions.LifeAssurancePremium, deductions.LifeAssurancePremium, "Premiums on your own or your spouse's life are deductible.");
        Add("Mortgage interest (owner-occupied home)", deductions.MortgageInterest, deductions.MortgageInterest, "Only interest, not capital repayments, on a loan for your own residence.");

        var statutory = deductions.PensionContributions + deductions.NhfContributions + deductions.NhisContributions +
                        deductions.LifeAssurancePremium + deductions.MortgageInterest;

        decimal cra = 0m;
        decimal rentRelief = 0m;

        if (usesCra)
        {
            var floor = Math.Max(pit.FlatCraAmount, incomeBase * pit.CraFloorRate);
            cra = RoundCurrency(floor + incomeBase * pit.CraRate);
            if (incomeBase > 0m)
            {
                lines.Insert(0, new DeductionLineDto
                {
                    Label = "Consolidated Relief Allowance (CRA)",
                    Claimed = cra,
                    Allowed = cra,
                    Note = $"Higher of ₦{pit.FlatCraAmount.ToString("N0", Naira)} or {pit.CraFloorRate.ToString("P0", TaxFormat.Culture)} of income, plus {pit.CraRate.ToString("P0", TaxFormat.Culture)} of income."
                });
            }

            if (deductions.AnnualRentPaid > 0m)
            {
                Add("Rent paid", deductions.AnnualRentPaid, 0m, "Rent is not separately deductible under this regime; CRA applies instead.");
            }
        }
        else if (deductions.AnnualRentPaid > 0m)
        {
            rentRelief = RoundCurrency(Math.Min(deductions.AnnualRentPaid * pit.RentReliefRate, pit.RentReliefCap));
            var capped = deductions.AnnualRentPaid * pit.RentReliefRate > pit.RentReliefCap;
            Add("Rent relief", deductions.AnnualRentPaid, rentRelief,
                $"{pit.RentReliefRate.ToString("P0", TaxFormat.Culture)} of annual rent, capped at ₦{pit.RentReliefCap.ToString("N0", Naira)}" +
                (capped ? " — your relief reached the cap." : ". Keep your tenancy agreement and receipts."));
        }

        return new PersonalReliefs(
            cra,
            rentRelief,
            RoundCurrency(statutory),
            RoundCurrency(cra + rentRelief + statutory),
            lines);
    }

    private static List<CalculationStepDto> BuildPersonalSteps(FullTaxReportDto report, PersonalIncomeTaxRules pit)
    {
        var steps = new List<CalculationStepDto>
        {
            new() { Label = "Gross income", Amount = report.GrossIncome, Description = "Total annual income from every source you declared." }
        };

        if (report.IncomeExcludedAsFinalTax > 0m)
        {
            steps.Add(new() { Label = "Less income already taxed at source (final tax)", Amount = report.IncomeExcludedAsFinalTax, Description = "Income on which withholding tax was deducted as a final tax is not taxed again." });
        }

        if (report.SelfEmployedExpenses > 0m)
        {
            steps.Add(new() { Label = "Less business expenses", Amount = report.SelfEmployedExpenses, Description = "Business and freelance income is taxed on profit, so allowable business expenses are deducted first." });
        }

        steps.Add(new() { Label = "Chargeable income", Amount = report.AdjustedGrossIncome, Description = "Income that is subject to personal income tax before reliefs." });

        if (report.ConsolidatedReliefAllowance > 0m)
        {
            steps.Add(new() { Label = "Less Consolidated Relief Allowance", Amount = report.ConsolidatedReliefAllowance, Description = $"Higher of ₦{pit.FlatCraAmount.ToString("N0", Naira)} or {pit.CraFloorRate.ToString("P0", TaxFormat.Culture)} of income, plus {pit.CraRate.ToString("P0", TaxFormat.Culture)} of income." });
        }

        if (report.RentRelief > 0m)
        {
            steps.Add(new() { Label = "Less rent relief", Amount = report.RentRelief, Description = $"{pit.RentReliefRate.ToString("P0", TaxFormat.Culture)} of annual rent paid, capped at ₦{pit.RentReliefCap.ToString("N0", Naira)}." });
        }

        if (report.TotalAllowableDeductions > 0m)
        {
            steps.Add(new() { Label = "Less eligible deductions", Amount = report.TotalAllowableDeductions, Description = "Pension, NHF, NHIS, life assurance and owner-occupied mortgage interest." });
        }

        steps.Add(new() { Label = "Taxable income", Amount = report.TaxableIncome, Description = "Income after reliefs and deductions, taxed using the progressive bands." });
        steps.Add(new() { Label = "Estimated annual tax payable", Amount = report.TaxPayable, Description = $"Sum of the tax charged in each band under {pit.RegimeName}." });
        return steps;
    }

    private static List<string> BuildPersonalTips(FullTaxReportDto report, AllowableDeductionInputDto deductions, PersonalIncomeTaxRules pit)
    {
        var tips = new List<string>();
        var usesRentRelief = string.Equals(pit.ReliefModel, ReliefModels.RentRelief, StringComparison.OrdinalIgnoreCase);

        if (report.TaxableIncome <= 0m && report.GrossIncome > 0m)
        {
            tips.Add("Your reliefs and deductions fully cover the income entered, so no tax is estimated. Keep the supporting records in case you are asked for them.");
        }

        if (usesRentRelief && deductions.AnnualRentPaid <= 0m && report.TaxableIncome > 0m)
        {
            tips.Add($"If you rent the home you live in, you can claim rent relief of {pit.RentReliefRate.ToString("P0", TaxFormat.Culture)} of the annual rent, up to ₦{pit.RentReliefCap.ToString("N0", Naira)}. You did not enter any rent.");
        }

        if (deductions.PensionContributions <= 0m && report.EmploymentIncome > 0m)
        {
            tips.Add("No pension contributions were entered. Employee contributions under the Pension Reform Act are deductible — check your payslip.");
        }

        if (deductions.NhfContributions <= 0m && deductions.NhisContributions <= 0m && report.EmploymentIncome > 0m)
        {
            tips.Add("NHF and NHIS contributions are deductible. If they appear on your payslip, add them to reduce your taxable income.");
        }

        if (deductions.LifeAssurancePremium <= 0m && report.TaxableIncome > 0m)
        {
            tips.Add("Life assurance or annuity premiums paid for yourself or your spouse are deductible if you have such a policy.");
        }

        if (report.SelfEmployedIncome > 0m && report.SelfEmployedExpenses <= 0m)
        {
            tips.Add("Business income is taxed on profit, not receipts. Record your business expenses (with receipts) so they can be deducted.");
        }

        if (report.TaxBandBreakdown.Count > 0)
        {
            var top = report.TaxBandBreakdown[^1];
            if (top.Rate > 0m)
            {
                tips.Add($"Your top marginal rate is {top.Rate.ToString("P0", TaxFormat.Culture)}: each additional ₦100,000 of eligible deductions reduces your tax by about ₦{(100_000m * top.Rate).ToString("N0", Naira)}.");
            }
        }

        return tips.Distinct().ToList();
    }

    private static List<string> BuildPersonalWarnings(FullTaxReportDto report, List<IncomeStreamDto> incomes, TaxCalculationRequestDto request)
    {
        var warnings = new List<string>();
        var hasNonEmployment = incomes.Any(s => !IsEmployment(s.Category) && s.AnnualAmount > 0m && !s.FinalTaxDeducted);

        if (report.EmploymentIncome > 0m && hasNonEmployment)
        {
            warnings.Add("PAYE deducted by your employer only covers salary. Tax on business, freelance, rental or investment income must be declared and paid through your annual self-assessment return.");
        }

        if (report.SelfEmployedIncome > 0m)
        {
            warnings.Add("As a self-employed person you are responsible for filing your own annual return with the tax authority of your state of residence.");
        }

        if (report.Vat.VatRequired)
        {
            warnings.Add(report.Vat.StatusMessage);
        }

        if (report.TaxPayable <= 0m && report.GrossIncome > 0m)
        {
            warnings.Add("A nil tax estimate does not remove the obligation to file an annual return where one is required.");
        }

        if (report.InvestmentIncome > 0m)
        {
            warnings.Add("Investment income was included in full. If withholding tax already deducted on it is a final tax, mark it as such — you may be overstating your liability.");
        }

        if (string.Equals(request.TaxpayerType, TaxpayerKinds.SME, StringComparison.OrdinalIgnoreCase) && request.Vat.AnnualTurnover <= 0m)
        {
            warnings.Add("Annual turnover was not provided, so the VAT check used business income instead.");
        }

        return warnings.Distinct().ToList();
    }

    private static IEnumerable<string> BuildPersonalAssumptions(FullTaxReportDto report, PersonalIncomeTaxRules pit)
    {
        yield return "You are resident in Nigeria for the full tax year; personal income tax is administered by your state of residence.";

        if (string.Equals(pit.ReliefModel, ReliefModels.RentRelief, StringComparison.OrdinalIgnoreCase))
        {
            yield return "The Consolidated Relief Allowance (CRA) no longer applies; rent relief and eligible deductions are used instead.";
        }

        if (report.EstimatedMonthlyPaye.HasValue)
        {
            yield return "Monthly PAYE is estimated on employment income only, assuming the same deductions spread evenly over 12 months.";
        }

        if (report.SelfEmployedIncome > 0m)
        {
            yield return "Business expenses are assumed to be wholly, exclusively and reasonably incurred for the business. Capital allowances are not modelled.";
        }
    }

    // -----------------------------------------------------------------------
    // Company income tax (limited liability companies)
    // -----------------------------------------------------------------------

    private static FullTaxReportDto BuildCompanyReport(
        TaxCalculationRequestDto request,
        TaxRuleSet rules,
        List<IncomeStreamDto> incomes,
        List<ExpenseItemDto> expenses,
        AllowableDeductionInputDto deductions)
    {
        var cit = rules.CompanyIncomeTax;
        var businessIncome = incomes.Where(s => IsSelfEmployed(s.Category)).Sum(s => s.AnnualAmount);
        var otherIncome = incomes.Where(s => !IsSelfEmployed(s.Category) && !s.FinalTaxDeducted).Sum(s => s.AnnualAmount);
        var turnover = request.Vat.AnnualTurnover > 0m ? SanitizeMoney(request.Vat.AnnualTurnover) : businessIncome;
        var fixedAssets = SanitizeMoney(request.FixedAssets);
        var totalExpenses = expenses.Sum(e => e.AnnualAmount);
        var assessableProfit = Math.Max(0m, turnover + otherIncome - totalExpenses);

        var isSmall = turnover <= cit.SmallCompanyTurnoverLimit &&
                      (cit.SmallCompanyFixedAssetLimit == null || fixedAssets <= cit.SmallCompanyFixedAssetLimit) &&
                      !(cit.SmallCompanyExcludesProfessionalServices && request.IsProfessionalServices);
        var isMedium = !isSmall && cit.MediumCompanyTurnoverLimit.HasValue && cit.MediumCompanyRate.HasValue &&
                       turnover <= cit.MediumCompanyTurnoverLimit.Value;

        var classification = isSmall ? "Small company" : isMedium ? "Medium company" : "Other company";
        var rate = isSmall ? cit.SmallCompanyRate : isMedium ? cit.MediumCompanyRate!.Value : cit.StandardRate;
        var incomeTax = RoundCurrency(assessableProfit * rate);
        var levyRate = isSmall && !cit.LevyAppliesToSmallCompanies ? 0m : cit.LevyRate;
        var levy = RoundCurrency(assessableProfit * levyRate);
        var total = incomeTax + levy;
        var grossIncome = turnover + otherIncome;

        var company = new CompanyTaxAssessmentDto
        {
            Classification = classification,
            Turnover = RoundCurrency(turnover),
            FixedAssets = RoundCurrency(fixedAssets),
            AllowableExpenses = RoundCurrency(totalExpenses),
            AssessableProfit = RoundCurrency(assessableProfit),
            IncomeTaxRate = rate,
            IncomeTax = incomeTax,
            LevyName = cit.LevyName,
            LevyRate = levyRate,
            Levy = levy,
            VerificationStatus = cit.VerificationStatus
        };

        var report = new FullTaxReportDto
        {
            TaxType = TaxTypes.CompanyIncomeTax,
            GrossIncome = RoundCurrency(grossIncome),
            AdjustedGrossIncome = RoundCurrency(assessableProfit),
            SelfEmployedIncome = RoundCurrency(turnover),
            SelfEmployedExpenses = RoundCurrency(totalExpenses),
            OtherIncome = RoundCurrency(otherIncome),
            Deductions = new AllowableDeductionInputDto(),
            DeductionAnalysis = expenses
                .Where(e => e.AnnualAmount > 0m)
                .Select(e => new DeductionLineDto { Label = e.Name, Claimed = e.AnnualAmount, Allowed = e.AnnualAmount, Note = "Treated as an allowable business expense." })
                .ToList(),
            TotalAllowableDeductions = RoundCurrency(totalExpenses),
            TotalDeductionsAndReliefs = RoundCurrency(totalExpenses),
            TaxableIncome = RoundCurrency(assessableProfit),
            TaxPayable = RoundCurrency(total),
            EffectiveTaxRate = grossIncome <= 0m ? 0m : Math.Round(total / grossIncome, 4, MidpointRounding.AwayFromZero),
            CompanyTax = company,
            Vat = BuildVatAssessment(true, turnover, rules.Vat),
            ApplicableRegime = $"{rules.Name} — company income tax"
        };

        report.CalculationSteps = new List<CalculationStepDto>
        {
            new() { Label = "Turnover", Amount = company.Turnover, Description = "Gross revenue from the company's trade for the year." },
            new() { Label = "Add other income", Amount = report.OtherIncome, Description = "Other taxable income received by the company." },
            new() { Label = "Less allowable expenses", Amount = company.AllowableExpenses, Description = "Operating costs, payroll, rent and other business expenses entered." },
            new() { Label = "Assessable profit", Amount = company.AssessableProfit, Description = "Profit on which company income tax is computed (capital allowances not modelled)." },
            new() { Label = $"Company income tax @ {rate.ToString("P0", TaxFormat.Culture)}", Amount = incomeTax, Description = $"Rate for a {classification.ToLowerInvariant()} under {rules.Name}." },
            new() { Label = $"{cit.LevyName} @ {levyRate.ToString("P0", TaxFormat.Culture)}", Amount = levy, Description = isSmall && !cit.LevyAppliesToSmallCompanies ? "Small companies are exempt." : $"{cit.LevyName} on assessable profit." },
            new() { Label = "Estimated total company tax", Amount = report.TaxPayable, Description = "Company income tax plus levy." }
        };

        report.OptimizationTips = new List<string>();
        if (!isSmall && turnover <= cit.SmallCompanyTurnoverLimit * 1.2m && turnover > cit.SmallCompanyTurnoverLimit)
        {
            report.OptimizationTips.Add($"Your turnover is just above the ₦{cit.SmallCompanyTurnoverLimit.ToString("N0", Naira)} small-company limit. Review whether all revenue is correctly recognised in this year — but never understate turnover.");
        }
        if (totalExpenses <= 0m)
        {
            report.OptimizationTips.Add("No expenses were entered. Company tax is charged on profit, so properly documented business expenses reduce your liability.");
        }
        report.OptimizationTips.Add("Capital allowances on qualifying assets (equipment, vehicles, buildings) can reduce assessable profit further; this estimate does not include them.");

        report.ComplianceWarnings = new List<string>
        {
            isSmall
                ? $"Small companies pay {cit.SmallCompanyRate.ToString("P0", TaxFormat.Culture)} company income tax but must still register, keep records and file annual returns."
                : "Company income tax returns, with audited accounts, must be filed annually with the Nigeria Revenue Service."
        };
        if (report.Vat.VatRequired)
        {
            report.ComplianceWarnings.Add(report.Vat.StatusMessage);
        }
        if (cit.SmallCompanyExcludesProfessionalServices && request.IsProfessionalServices && turnover <= cit.SmallCompanyTurnoverLimit)
        {
            report.ComplianceWarnings.Add("Companies providing professional services do not qualify for the small-company exemption, even below the turnover limit.");
        }
        if (cit.LargeCompanyAdviceTurnover.HasValue && turnover >= cit.LargeCompanyAdviceTurnover.Value)
        {
            report.ComplianceWarnings.Add("At this turnover, minimum effective tax rules and other large-company provisions may apply. Engage a qualified tax adviser.");
        }
        if (deductions.PensionContributions + deductions.NhfContributions + deductions.NhisContributions + deductions.LifeAssurancePremium + deductions.MortgageInterest + deductions.AnnualRentPaid > 0m)
        {
            report.ComplianceWarnings.Add("Personal reliefs (pension, NHF, rent, etc.) do not apply to company tax and were ignored. Directors' personal tax is assessed separately.");
        }

        report.Assumptions.Add("The company's financial year ends on 31 December and it is resident in Nigeria.");
        report.Assumptions.Add("All expenses entered are allowable for tax. Capital allowances, losses brought forward and non-deductible items are not modelled.");
        if (cit.VerificationStatus != VerificationStatuses.Verified)
        {
            report.Assumptions.Add("Company size thresholds are pending validation against the official text — confirm your classification with the Nigeria Revenue Service or a tax adviser.");
        }

        return report;
    }

    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------

    private static VatAssessmentDto BuildVatAssessment(bool applicable, decimal turnover, VatRules rules)
    {
        if (!applicable)
        {
            return new VatAssessmentDto
            {
                Applicable = false,
                Threshold = rules.RegistrationThreshold,
                VatRate = rules.Rate,
                StatusMessage = "VAT does not apply to employment income.",
                VerificationStatus = rules.VerificationStatus
            };
        }

        var required = rules.ExemptAtThreshold ? turnover > rules.RegistrationThreshold : turnover >= rules.RegistrationThreshold;
        var thresholdText = $"₦{rules.RegistrationThreshold.ToString("N0", Naira)}";
        var caution = rules.VerificationStatus == VerificationStatuses.Verified
            ? string.Empty
            : " This threshold is pending validation — confirm with the Nigeria Revenue Service.";

        return new VatAssessmentDto
        {
            Applicable = true,
            AnnualTurnover = RoundCurrency(turnover),
            Threshold = RoundCurrency(rules.RegistrationThreshold),
            VatRate = rules.Rate,
            VatRequired = required,
            StatusMessage = (required
                ? $"Turnover is above the {thresholdText} small-business threshold, so you should register for VAT, charge {rules.Rate.ToString("P1", TaxFormat.Culture)} on taxable supplies and file monthly returns."
                : $"Turnover is within the {thresholdText} small-business threshold, so you are likely exempt from charging VAT. Monitor turnover monthly.") + caution,
            VerificationStatus = rules.VerificationStatus
        };
    }

    private static List<string> BuildNextSteps(FullTaxReportDto report, TaxRuleSet rules, List<ExpenseItemDto> expenses)
    {
        var audiences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (report.TaxType == TaxTypes.CompanyIncomeTax)
        {
            audiences.Add("Company");
        }
        else
        {
            if (report.EmploymentIncome > 0m) audiences.Add("Employee");
            if (report.SelfEmployedIncome > 0m || report.OtherIncome > 0m || report.InvestmentIncome > 0m) audiences.Add("SelfEmployed");
        }
        if (report.Vat.VatRequired) audiences.Add("VatRegistered");
        if (expenses.Any(e => Is(e.Category, ExpenseCategories.Payroll) && e.AnnualAmount > 0m)) audiences.Add("Employer");

        var steps = rules.Deadlines
            .Where(d => d.AppliesTo.Any(audiences.Contains))
            .Select(d => $"{d.Title}: {d.Description}")
            .ToList();

        steps.Add(report.TaxType == TaxTypes.CompanyIncomeTax
            ? "Keep invoices, receipts, payroll records, bank statements and financial statements for at least six years."
            : "Keep payslips, receipts, tenancy agreements and contribution statements for at least six years.");
        if (report.TaxPayable > 0m && report.GrossIncome > 0m && report.EffectiveTaxRate >= 0.2m)
        {
            steps.Add("Your liability is significant — a qualified tax adviser can confirm the figures before you file.");
        }

        return steps.Distinct().ToList();
    }

    private static List<TaxBandChargeDto> CalculateBandBreakdown(decimal taxableIncome, IEnumerable<TaxBandRule> bands)
    {
        var result = new List<TaxBandChargeDto>();
        var lowerLimit = 0m;

        foreach (var band in bands.OrderBy(b => b.UpperLimit ?? decimal.MaxValue))
        {
            var upperLimit = band.UpperLimit ?? decimal.MaxValue;
            if (taxableIncome <= lowerLimit)
            {
                break;
            }

            var portion = Math.Min(taxableIncome, upperLimit) - lowerLimit;
            if (portion > 0m)
            {
                result.Add(new TaxBandChargeDto
                {
                    Label = BuildBandLabel(lowerLimit, band.UpperLimit, band.Rate),
                    LowerLimit = lowerLimit,
                    UpperLimit = band.UpperLimit,
                    TaxableAmount = RoundCurrency(portion),
                    Rate = band.Rate,
                    TaxAmount = RoundCurrency(portion * band.Rate)
                });
            }

            lowerLimit = upperLimit;
        }

        return result;
    }

    public static string BuildBandLabel(decimal lowerLimit, decimal? upperLimit, decimal rate)
    {
        return upperLimit == null
            ? $"Above ₦{lowerLimit.ToString("N0", Naira)} @ {rate.ToString("P0", Naira)}"
            : $"₦{lowerLimit.ToString("N0", Naira)} – ₦{upperLimit.Value.ToString("N0", Naira)} @ {rate.ToString("P0", Naira)}";
    }

    private static bool Is(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsEmployment(string? category) =>
        Is(category, IncomeCategories.Employment) || Is(category, IncomeCategories.Bonus) || Is(category, IncomeCategories.Allowance);

    private static bool IsSelfEmployed(string? category) =>
        Is(category, IncomeCategories.Business) || Is(category, IncomeCategories.Freelance);

    private static bool IsBusinessExpense(string? category) =>
        Is(category, ExpenseCategories.Business) || Is(category, ExpenseCategories.Operating) ||
        Is(category, ExpenseCategories.CostOfSales) || Is(category, ExpenseCategories.Professional) ||
        Is(category, ExpenseCategories.Payroll) || Is(category, ExpenseCategories.RentAndUtilities);

    private static decimal SanitizeMoney(decimal amount) => amount < 0m ? 0m : amount;

    private static decimal RoundCurrency(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
