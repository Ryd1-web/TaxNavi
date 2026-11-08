using FluentValidation;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;

namespace TaxNavi.Application.Validators;

public class FullTaxCalculationRequestValidator : AbstractValidator<FullTaxCalculationRequestDto>
{
    public FullTaxCalculationRequestValidator(ITaxRuleProvider ruleProvider)
    {
        RuleFor(x => x.Input).SetValidator(new TaxCalculationRequestValidator(ruleProvider));
    }
}
