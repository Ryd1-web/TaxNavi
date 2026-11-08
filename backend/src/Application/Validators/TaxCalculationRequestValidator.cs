using FluentValidation;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;

namespace TaxNavi.Application.Validators;

public class TaxCalculationRequestValidator : AbstractValidator<TaxCalculationRequestDto>
{
    public TaxCalculationRequestValidator(ITaxRuleProvider ruleProvider)
    {
        RuleFor(x => x.TaxYear)
            .Must(year => year == null || ruleProvider.FindRuleSet(year.Value) != null)
            .WithMessage(_ => $"Tax year is not supported. Choose one of: {string.Join(", ", ruleProvider.SupportedTaxYears)}.");

        RuleFor(x => x.TaxpayerType)
            .Must(TaxpayerKinds.IsSupported)
            .WithMessage("Taxpayer type must be Individual, SelfEmployed, or SME.");

        RuleFor(x => x.BusinessStructure)
            .Must(BusinessStructures.IsSupported)
            .WithMessage("Business structure must be SoleProprietor or LimitedCompany.");

        RuleFor(x => x.BusinessType).MaximumLength(100);

        RuleFor(x => x.IncomeSources)
            .NotEmpty().WithMessage("Add at least one source of income.")
            .Must(list => list.Count <= 50).WithMessage("A maximum of 50 income sources is supported.");

        RuleFor(x => x.IncomeSources)
            .Must(list => list.Sum(item => Math.Max(0m, item.AnnualAmount)) > 0m)
            .When(x => x.IncomeSources.Count > 0)
            .WithMessage("Enter an amount for at least one income source.");

        RuleForEach(x => x.IncomeSources).SetValidator(new IncomeStreamValidator());

        RuleFor(x => x.Expenses)
            .Must(list => list.Count <= 50).WithMessage("A maximum of 50 expense lines is supported.");
        RuleForEach(x => x.Expenses).SetValidator(new ExpenseItemValidator());

        RuleFor(x => x.AllowableDeductions.PensionContributions).InclusiveBetween(0, MoneyLimits.Max).WithMessage("Pension contributions must be zero or more.");
        RuleFor(x => x.AllowableDeductions.NhfContributions).InclusiveBetween(0, MoneyLimits.Max).WithMessage("NHF contributions must be zero or more.");
        RuleFor(x => x.AllowableDeductions.NhisContributions).InclusiveBetween(0, MoneyLimits.Max).WithMessage("NHIS contributions must be zero or more.");
        RuleFor(x => x.AllowableDeductions.LifeAssurancePremium).InclusiveBetween(0, MoneyLimits.Max).WithMessage("Life assurance premium must be zero or more.");
        RuleFor(x => x.AllowableDeductions.MortgageInterest).InclusiveBetween(0, MoneyLimits.Max).WithMessage("Mortgage interest must be zero or more.");
        RuleFor(x => x.AllowableDeductions.AnnualRentPaid).InclusiveBetween(0, MoneyLimits.Max).WithMessage("Rent paid must be zero or more.");
        RuleFor(x => x.Vat.AnnualTurnover).InclusiveBetween(0, MoneyLimits.Max).WithMessage("Annual turnover must be zero or more.");
        RuleFor(x => x.FixedAssets).InclusiveBetween(0, MoneyLimits.Max).WithMessage("Fixed assets must be zero or more.");

        When(x => string.Equals(x.TaxpayerType, TaxpayerKinds.SME, StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.BusinessStructure)
                .NotEmpty()
                .WithMessage("Tell us whether the business is a sole proprietorship or a limited company.");

            RuleFor(x => x.Vat.AnnualTurnover)
                .GreaterThan(0)
                .WithMessage("Annual turnover is required for business tax and VAT checks.");
        });
    }
}
