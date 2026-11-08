namespace TaxNavi.Application.Validators;

using FluentValidation;
using DTOs;
using Domain.Entities;

public class VerifyPaymentValidator : AbstractValidator<VerifyPaymentDto>
{
    public VerifyPaymentValidator()
    {
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(100).WithMessage("Payment reference is required.");
    }
}
