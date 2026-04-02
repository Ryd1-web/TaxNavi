namespace TaxNavi.Application.DTOs;

public class IndividualTaxAssessmentDto
{
    public Guid UserId { get; set; }
    public decimal MonthlySalary { get; set; }
    public decimal AnnualBonus { get; set; }
    public decimal Allowances { get; set; }
    public decimal BusinessIncome { get; set; }
    public decimal FreelanceIncome { get; set; }
    public decimal InvestmentIncome { get; set; }
    public decimal RentPaid { get; set; }
    public decimal LoanInterest { get; set; }
    public decimal OtherExpenses { get; set; }
}

public class SMEAssessmentDto
{
    public Guid UserId { get; set; }
    public decimal AnnualTurnover { get; set; }
    public decimal OperatingExpenses { get; set; }
    public decimal PayrollCost { get; set; }
    public decimal RentAndUtilities { get; set; }
    public decimal Assets { get; set; }
    public decimal VatableIncome { get; set; }
    public decimal WhtExposure { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
}

public class TaxReportDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public decimal TaxLiability { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal AllowableDeductions { get; set; }
    public decimal ProjectedSavings { get; set; }
    public string PdfUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class PaymentInitiationDto
{
    public decimal Amount { get; set; }
    public Guid TaxReportId { get; set; }
}

public class PaymentVerificationDto
{
    public string Reference { get; set; } = string.Empty;
}

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class DashboardDto
{
    public UserProfileDto? User { get; set; }
    public List<TaxReportDto> RecentReports { get; set; } = new();
    public decimal TotalTaxPaid { get; set; }
}

public class GeneratePdfRequest
{
    public string ReportType { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal AllowableDeductions { get; set; }
    public decimal TaxLiability { get; set; }
    public decimal ProjectedSavings { get; set; }
}

public class GeneratePdfReportDto
{
    public string ReportType { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal AllowableDeductions { get; set; }
    public decimal TaxLiability { get; set; }
    public decimal ProjectedSavings { get; set; }
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class InitiatePaymentDto
{
    public decimal Amount { get; set; }
    public Guid TaxReportId { get; set; }
    public Guid UserId { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class VerifyPaymentDto
{
    public string Reference { get; set; } = string.Empty;
}

public class PaymentVerificationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public PaymentData? Data { get; set; }
}

public class PaymentData
{
    public string Reference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
