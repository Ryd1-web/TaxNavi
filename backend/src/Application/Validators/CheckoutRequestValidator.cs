namespace TaxNavi.Application.Validators;

using FluentValidation;
using DTOs;
using Domain.Entities;

public class CheckoutRequestValidator : AbstractValidator<CheckoutRequestDto>
{
    public CheckoutRequestValidator(TaxNavi.Application.Services.ITaxRuleProvider ruleProvider)
    {
        RuleFor(x => x.Purpose)
            .Must(p => p is PaymentPurposes.PremiumReport or PaymentPurposes.SmeSubscription)
            .WithMessage("Purpose must be PremiumReport or SmeSubscription.");

        RuleFor(x => x)
            .Must(x => x.TaxReportId.HasValue || x.Calculation != null)
            .When(x => x.Purpose == PaymentPurposes.PremiumReport)
            .WithName("TaxReportId")
            .WithMessage("Choose the report you want to unlock.");

        RuleFor(x => x.Calculation!)
            .SetValidator(new TaxCalculationRequestValidator(ruleProvider))
            .When(x => x.Calculation != null);

        RuleFor(x => x.Email)
            .EmailAddress().MaximumLength(254)
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Enter a valid email address.");

        RuleFor(x => x.AccessToken).MaximumLength(128);
    }
}
