using System.Globalization;
using System.Text;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Models;

namespace TaxNavi.Application.Services;

/// <summary>
/// Grounded tax assistant. Answers are composed only from the versioned rule set and the
/// deterministic engine's output — it never produces rates, thresholds or rules of its own.
/// Questions outside the approved topics are routed to professional advice.
/// </summary>
public class GroundedTaxAssistantService : ITaxAssistantService
{
    private static readonly CultureInfo Inv = TaxFormat.Culture;
    private readonly ITaxRuleProvider _rules;
    private readonly ITaxCalculationEngine _engine;

    private static readonly string[] Suggested =
    {
        "How are personal income tax bands applied?",
        "What deductions can I claim?",
        "How does rent relief work?",
        "Do I need to register for VAT?",
        "When do I file my annual return?",
        "Explain my result"
    };

    // Topics that need case-specific professional judgement.
    private static readonly (string[] Keywords, string Topic)[] EscalationTopics =
    {
        (new[] { "penalt", "fine", "late payment", "interest on late" }, "Penalties"),
        (new[] { "dispute", "objection", "appeal", "tribunal", "audit", "investigat" }, "Disputes and audits"),
        (new[] { "crypto", "bitcoin", "digital asset" }, "Digital assets"),
        (new[] { "capital gain", "sell my house", "sold shares", "sale of land" }, "Capital gains"),
        (new[] { "non-resident", "nonresident", "abroad", "foreign income", "diaspora", "double tax" }, "Residency and foreign income"),
        (new[] { "transfer pricing", "multinational", "group restructur" }, "Group and international tax"),
        (new[] { "evade", "evasion", "hide income", "avoid paying" }, "Compliance")
    };

    public GroundedTaxAssistantService(ITaxRuleProvider rules, ITaxCalculationEngine engine)
    {
        _rules = rules;
        _engine = engine;
    }

    public AssistantAnswerDto Answer(AssistantQuestionDto question)
    {
        var taxYear = question.TaxYear ?? question.Calculation?.TaxYear ?? _rules.DefaultTaxYear;
        var rules = _rules.FindRuleSet(taxYear) ?? _rules.FindRuleSet(_rules.DefaultTaxYear)!;
        var q = (question.Question ?? string.Empty).Trim().ToLowerInvariant();

        foreach (var (keywords, topic) in EscalationTopics)
        {
            if (keywords.Any(q.Contains))
            {
                return Build(rules, topic,
                    $"{topic} depend on facts TaxNavi cannot assess from a calculator. Please speak to a chartered tax practitioner (CITN member) or contact the Nigeria Revenue Service or your State Internal Revenue Service directly. TaxNavi will not guess on this topic.",
                    requiresAdvice: true, grounding: "Escalation policy");
            }
        }

        FullTaxReportDto? report = null;
        if (question.Calculation != null && question.Calculation.IncomeSources.Count > 0)
        {
            question.Calculation.TaxYear = taxYear;
            report = _engine.GenerateFullReport(question.Calculation);
        }

        if (Matches(q, "my result", "my tax", "explain", "why is", "how much", "my estimate") && report != null)
        {
            return Build(rules, "Your result", ExplainReport(report), grounding: "Your calculation from the TaxNavi engine");
        }
        if (Matches(q, "rent"))
        {
            return Build(rules, "Rent relief", ExplainRent(rules));
        }
        if (Matches(q, "cra", "consolidated relief"))
        {
            return Build(rules, "Consolidated Relief Allowance", ExplainCra(rules));
        }
        if (Matches(q, "vat", "value added"))
        {
            return Build(rules, "VAT", ExplainVat(rules), statusOverride: rules.Vat.VerificationStatus);
        }
        if (Matches(q, "company", "cit", "limited", "small business", "small company", "development levy", "ltd"))
        {
            return Build(rules, "Company income tax", ExplainCompany(rules), statusOverride: rules.CompanyIncomeTax.VerificationStatus);
        }
        if (Matches(q, "deadline", "file", "filing", "return", "when do i", "due date", "calendar"))
        {
            return Build(rules, "Filing deadlines", ExplainDeadlines(rules));
        }
        if (Matches(q, "paye", "salary", "employer", "payslip"))
        {
            return Build(rules, "PAYE", ExplainPaye(rules, report));
        }
        if (Matches(q, "deduct", "relief", "pension", "nhf", "nhis", "life assurance", "insurance", "mortgage", "reduce my tax", "save"))
        {
            return Build(rules, "Eligible deductions", ExplainDeductions(rules));
        }
        if (Matches(q, "freelanc", "self-employ", "self employ", "business income", "side hustle", "sole"))
        {
            return Build(rules, "Self-employed income", ExplainSelfEmployed(rules));
        }
        if (Matches(q, "effective", "marginal"))
        {
            return Build(rules, "Effective vs marginal rate", ExplainRates(report));
        }
        if (Matches(q, "band", "rate", "bracket", "percent", "%", "how is tax calculated", "progressive"))
        {
            return Build(rules, "Tax bands", ExplainBands(rules));
        }
        if (Matches(q, "nrs", "firs", "who collects", "revenue service", "joint revenue", "state irs", "lirs"))
        {
            return Build(rules, "Tax authorities", "The Nigeria Revenue Service (NRS, formerly FIRS) administers federal taxes such as company income tax and VAT. Personal income tax for residents — including PAYE and self-assessment — is administered by the Internal Revenue Service of the state where you live. The Joint Revenue Board coordinates between federal and state tax authorities.", grounding: "TaxNavi reference content");
        }

        return Build(rules, "Not covered",
            "I can only answer questions covered by TaxNavi's reviewed tax rules, so I won't guess. Try one of the suggested questions, or speak to a qualified tax professional for anything specific to your situation.",
            requiresAdvice: true, grounding: "Escalation policy");
    }

    private static bool Matches(string q, params string[] keywords) => keywords.Any(q.Contains);

    private static AssistantAnswerDto Build(TaxRuleSet rules, string topic, string answer, bool requiresAdvice = false, string? grounding = null, string? statusOverride = null) => new()
    {
        Topic = topic,
        Answer = answer,
        RequiresProfessionalAdvice = requiresAdvice,
        Grounding = grounding ?? $"{rules.Name} rule set (tax years from {rules.FirstTaxYear})",
        VerificationStatus = statusOverride ?? rules.VerificationStatus,
        Sources = rules.Sources,
        SuggestedQuestions = Suggested.Where(s => !string.Equals(s, topic, StringComparison.OrdinalIgnoreCase)).Take(4).ToList()
    };

    private static string Money(decimal value) => $"₦{value.ToString("N0", Inv)}";
    private static string Pct(decimal value) => value.ToString(value * 100 % 1 == 0 ? "P0" : "P1", Inv);

    private static string ExplainBands(TaxRuleSet rules)
    {
        var sb = new StringBuilder($"Under {rules.Name}, personal income tax is progressive — each slice of your taxable income is taxed at its own rate:\n");
        decimal lower = 0m;
        foreach (var band in rules.PersonalIncomeTax.Bands.OrderBy(b => b.UpperLimit ?? decimal.MaxValue))
        {
            sb.Append(band.UpperLimit == null
                ? $"• Above {Money(lower)}: {Pct(band.Rate)}\n"
                : $"• {Money(lower)} – {Money(band.UpperLimit.Value)}: {Pct(band.Rate)}\n");
            lower = band.UpperLimit ?? lower;
        }
        sb.Append("Moving into a higher band only increases the rate on the portion above that band's threshold, not on all your income.");
        return sb.ToString();
    }

    private static string ExplainRent(TaxRuleSet rules)
    {
        var pit = rules.PersonalIncomeTax;
        if (pit.ReliefModel == ReliefModels.RentRelief)
        {
            return $"If you pay rent on the home you live in, you can deduct {Pct(pit.RentReliefRate)} of the annual rent, up to a maximum of {Money(pit.RentReliefCap)}. For example, rent of {Money(1_800_000m)} gives relief of {Money(Math.Min(1_800_000m * pit.RentReliefRate, pit.RentReliefCap))}. You must declare the rent accurately and keep your tenancy agreement and receipts.";
        }
        return $"For tax years under {rules.Name}, rent is not separately deductible. The Consolidated Relief Allowance covers general living costs instead.";
    }

    private static string ExplainCra(TaxRuleSet rules)
    {
        var pit = rules.PersonalIncomeTax;
        return pit.ReliefModel == ReliefModels.Cra
            ? $"Under {rules.Name}, the Consolidated Relief Allowance is the higher of {Money(pit.FlatCraAmount)} or {Pct(pit.CraFloorRate)} of income, plus {Pct(pit.CraRate)} of income. It is deducted before the tax bands are applied."
            : $"The Consolidated Relief Allowance does not apply under {rules.Name}. It was replaced by a 0% band on the first slice of taxable income and by rent relief ({Pct(pit.RentReliefRate)} of rent, capped at {Money(pit.RentReliefCap)}).";
    }

    private static string ExplainDeductions(TaxRuleSet rules)
    {
        var pit = rules.PersonalIncomeTax;
        var sb = new StringBuilder($"Under {rules.Name}, TaxNavi applies these eligible deductions:\n");
        sb.Append("• Pension contributions under the Pension Reform Act\n");
        sb.Append("• National Housing Fund (NHF) contributions\n");
        sb.Append("• National health insurance (NHIS) contributions\n");
        sb.Append("• Life assurance or annuity premiums for you or your spouse\n");
        sb.Append("• Interest (not capital) on a loan for your own owner-occupied home\n");
        sb.Append(pit.ReliefModel == ReliefModels.RentRelief
            ? $"• Rent relief: {Pct(pit.RentReliefRate)} of annual rent, capped at {Money(pit.RentReliefCap)}\n"
            : $"• The Consolidated Relief Allowance (higher of {Money(pit.FlatCraAmount)} or {Pct(pit.CraFloorRate)} of income, plus {Pct(pit.CraRate)})\n");
        sb.Append("Self-employed people also deduct business expenses that are wholly and exclusively for the business. Keep evidence for every claim.");
        return sb.ToString();
    }

    private static string ExplainVat(TaxRuleSet rules)
    {
        var vat = rules.Vat;
        var caution = vat.VerificationStatus == VerificationStatuses.Verified
            ? string.Empty
            : " Note: published guidance on this threshold is inconsistent, so TaxNavi flags it as pending validation — confirm with the Nigeria Revenue Service before relying on it.";
        return $"VAT is charged at {Pct(vat.Rate)} on taxable goods and services. TaxNavi treats businesses with annual turnover {(vat.ExemptAtThreshold ? "not exceeding" : "below")} {Money(vat.RegistrationThreshold)} as small businesses exempt from charging VAT. Above that, you should register, charge VAT on invoices and file monthly returns. VAT does not apply to salary income.{caution}";
    }

    private static string ExplainCompany(TaxRuleSet rules)
    {
        var cit = rules.CompanyIncomeTax;
        var small = $"turnover {(cit.SmallCompanyFixedAssetLimit.HasValue ? "not exceeding " : "below ")}{Money(cit.SmallCompanyTurnoverLimit)}" +
                    (cit.SmallCompanyFixedAssetLimit.HasValue ? $" and fixed assets not exceeding {Money(cit.SmallCompanyFixedAssetLimit.Value)}" : string.Empty);
        var sb = new StringBuilder($"Under {rules.Name}, a limited company is a small company if it has {small}");
        sb.Append(cit.SmallCompanyExcludesProfessionalServices ? " and does not provide professional services. " : ". ");
        sb.Append($"Small companies pay {Pct(cit.SmallCompanyRate)} company income tax. ");
        if (cit.MediumCompanyTurnoverLimit.HasValue && cit.MediumCompanyRate.HasValue)
        {
            sb.Append($"Companies with turnover up to {Money(cit.MediumCompanyTurnoverLimit.Value)} pay {Pct(cit.MediumCompanyRate.Value)}. ");
        }
        sb.Append($"Other companies pay {Pct(cit.StandardRate)} on assessable profit");
        sb.Append(cit.LevyRate > 0m ? $", plus a {cit.LevyName.ToLowerInvariant()} of {Pct(cit.LevyRate)}." : ".");
        if (cit.VerificationStatus != VerificationStatuses.Verified)
        {
            sb.Append(" These size thresholds are pending validation against the official text — confirm your classification with the NRS or a tax adviser.");
        }
        return sb.ToString();
    }

    private static string ExplainDeadlines(TaxRuleSet rules)
    {
        if (rules.Deadlines.Count == 0)
        {
            return "No filing deadlines are configured for this tax year. Check with your tax authority.";
        }
        var sb = new StringBuilder("Key deadlines TaxNavi tracks:\n");
        foreach (var d in rules.Deadlines)
        {
            sb.Append($"• {d.Title} — {d.Description}\n");
        }
        sb.Append("If a deadline falls on a weekend or public holiday, confirm the exact date with your tax authority.");
        return sb.ToString();
    }

    private static string ExplainPaye(TaxRuleSet rules, FullTaxReportDto? report)
    {
        var deadline = rules.Deadlines.FirstOrDefault(d => d.Id == "paye-remittance");
        var sb = new StringBuilder("PAYE (Pay As You Earn) is personal income tax deducted from your salary by your employer each month and paid to your state's Internal Revenue Service. It uses the same bands and deductions as annual personal income tax, spread across the year.");
        if (deadline != null)
        {
            sb.Append($" {deadline.Description}");
        }
        if (report?.EstimatedMonthlyPaye is decimal paye)
        {
            sb.Append($" Based on your figures, your estimated monthly PAYE is {Money(paye)}.");
        }
        sb.Append(" PAYE only covers salary — other income must be declared on your annual return.");
        return sb.ToString();
    }

    private static string ExplainSelfEmployed(TaxRuleSet rules)
    {
        return $"If you earn from freelancing or your own unincorporated business, you pay personal income tax on your profit — receipts minus allowable business expenses — using the same bands as employees. No one deducts it for you, so you must file a self-assessment return with your state's Internal Revenue Service. The same personal deductions apply under {rules.Name}. If your turnover crosses the VAT threshold, VAT obligations may also apply.";
    }

    private static string ExplainRates(FullTaxReportDto? report)
    {
        var text = "Your effective rate is total tax divided by total income — the average share of your income paid as tax. Your marginal rate is the rate on your last naira of taxable income, i.e. your highest band.";
        if (report != null && report.TaxBandBreakdown.Count > 0)
        {
            text += $" For your figures: effective rate {report.EffectiveTaxRate.ToString("P2", Inv)}, marginal rate {report.TaxBandBreakdown[^1].Rate.ToString("P0", Inv)}.";
        }
        return text;
    }

    private static string ExplainReport(FullTaxReportDto report)
    {
        var sb = new StringBuilder($"For tax year {report.TaxYear}, your gross income is {Money(report.GrossIncome)}. ");
        if (report.TaxType == TaxTypes.CompanyIncomeTax && report.CompanyTax != null)
        {
            var c = report.CompanyTax;
            sb.Append($"As a {c.Classification.ToLowerInvariant()}, company tax is charged on assessable profit of {Money(c.AssessableProfit)} at {c.IncomeTaxRate.ToString("P0", Inv)}");
            sb.Append(c.Levy > 0m ? $", plus {c.LevyName.ToLowerInvariant()} of {Money(c.Levy)}" : string.Empty);
            sb.Append($", giving an estimated {Money(report.TaxPayable)}.");
            return sb.ToString();
        }

        sb.Append($"After {Money(report.TotalDeductionsAndReliefs)} of reliefs and deductions");
        if (report.SelfEmployedExpenses > 0m)
        {
            sb.Append($" and {Money(report.SelfEmployedExpenses)} of business expenses");
        }
        sb.Append($", your taxable income is {Money(report.TaxableIncome)}. ");
        if (report.TaxBandBreakdown.Count > 0)
        {
            sb.Append($"That income falls across {report.TaxBandBreakdown.Count} band(s), with your highest slice taxed at {report.TaxBandBreakdown[^1].Rate.ToString("P0", Inv)}. ");
        }
        sb.Append($"Estimated annual tax: {Money(report.TaxPayable)} (effective rate {report.EffectiveTaxRate.ToString("P2", Inv)}).");
        return sb.ToString();
    }
}
