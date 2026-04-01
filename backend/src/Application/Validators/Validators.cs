namespace TaxNavi.Application.Validators;

using FluentValidation;
using DTOs;
using Domain.Interfaces;

public class IndividualTaxAssessmentValidator : AbstractValidator<IndividualTaxAssessmentDto>
{
    public IndividualTaxAssessmentValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
        RuleFor(x => x.MonthlySalary).GreaterThanOrEqualTo(0).WithMessage("Monthly salary cannot be negative");
        RuleFor(x => x.AnnualBonus).GreaterThanOrEqualTo(0).WithMessage("Annual bonus cannot be negative");
        RuleFor(x => x.Allowances).GreaterThanOrEqualTo(0).WithMessage("Allowances cannot be negative");
        RuleFor(x => x.BusinessIncome).GreaterThanOrEqualTo(0).WithMessage("Business income cannot be negative");
        RuleFor(x => x.FreelanceIncome).GreaterThanOrEqualTo(0).WithMessage("Freelance income cannot be negative");
        RuleFor(x => x.InvestmentIncome).GreaterThanOrEqualTo(0).WithMessage("Investment income cannot be negative");
        RuleFor(x => x.RentPaid).GreaterThanOrEqualTo(0).WithMessage("Rent paid cannot be negative");
        RuleFor(x => x.LoanInterest).GreaterThanOrEqualTo(0).WithMessage("Loan interest cannot be negative");
        RuleFor(x => x.OtherExpenses).GreaterThanOrEqualTo(0).WithMessage("Other expenses cannot be negative");
    }
}

public class SMEAssessmentValidator : AbstractValidator<SMEAssessmentDto>
{
    public SMEAssessmentValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
        RuleFor(x => x.AnnualTurnover).GreaterThan(0).WithMessage("Annual turnover must be greater than 0");
        RuleFor(x => x.OperatingExpenses).GreaterThanOrEqualTo(0).WithMessage("Operating expenses cannot be negative");
        RuleFor(x => x.PayrollCost).GreaterThanOrEqualTo(0).WithMessage("Payroll cost cannot be negative");
        RuleFor(x => x.BusinessName).NotEmpty().WithMessage("Business name is required");
        RuleFor(x => x.BusinessType).NotEmpty().WithMessage("Business type is required");
    }
}

public class PaymentInitiationValidator : AbstractValidator<PaymentInitiationDto>
{
    public PaymentInitiationValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Payment amount must be greater than 0");
        RuleFor(x => x.TaxReportId).NotEmpty().WithMessage("Tax report ID is required");
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be valid");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MinimumLength(2).WithMessage("First name must be at least 2 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MinimumLength(2).WithMessage("Last name must be at least 2 characters");

        RuleFor(x => x.UserType)
            .NotEmpty().WithMessage("User type is required")
            .Must(x => x == "Individual" || x == "SME").WithMessage("User type must be either Individual or SME");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be valid");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}
