using FluentValidation;
using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Validators;

public class IncomeStreamValidator : AbstractValidator<IncomeStreamDto>
{
    public IncomeStreamValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Each income source needs a name (max 100 characters).");
        RuleFor(x => x.Category).NotEmpty().WithMessage("Income source category is required.");
        RuleFor(x => x.AnnualAmount).InclusiveBetween(0, MoneyLimits.Max).WithMessage("Income amounts must be zero or more.");
    }
}
