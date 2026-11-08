using FluentValidation;
using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Validators;

public class ExpenseItemValidator : AbstractValidator<ExpenseItemDto>
{
    public ExpenseItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Each expense needs a name (max 100 characters).");
        RuleFor(x => x.Category).NotEmpty().WithMessage("Expense category is required.");
        RuleFor(x => x.AnnualAmount).InclusiveBetween(0, MoneyLimits.Max).WithMessage("Expense amounts must be zero or more.");
    }
}
