namespace TaxNavi.Domain.Interfaces;

using Entities;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
    Task SaveChangesAsync();
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
}

public interface ITaxProfileRepository : IRepository<TaxProfile>
{
    Task<IEnumerable<TaxProfile>> GetByUserIdAsync(Guid userId);
}

public interface ITaxReportRepository : IRepository<TaxReport>
{
    Task<IEnumerable<TaxReport>> GetByUserIdAsync(Guid userId);
}

public interface IPaymentRepository : IRepository<Payment>
{
    Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId);
    Task<Payment?> GetByReferenceAsync(string reference);
}

public interface ITaxCalculationService
{
    decimal CalculatePIT(decimal grossIncome, decimal allowableDeductions);
    decimal CalculateConsolidatedRelief(decimal grossIncome);
    decimal ApplyTaxBands(decimal taxableIncome);
    Task<TaxCalculationResult> CalculateIndividualTax(decimal monthlySalary, decimal annualBonus, decimal allowances, decimal businessIncome, decimal freelanceIncome, decimal investmentIncome, decimal rentPaid, decimal loanInterest, decimal otherExpenses);
    Task<SMETaxCalculationResult> CalculateSMETax(decimal annualTurnover, decimal operatingExpenses, decimal payrollCost, decimal assets, decimal varableIncome, decimal whtExposure);
    Task<TaxCalculationResult> CalculateIndividualTaxAsync(IndividualTaxInput input);
    Task<SMETaxCalculationResult> CalculateSMETaxAsync(SMETaxInput input);
}

public interface IAiTaxAdvisorService
{
    Task<string> GenerateHumanReadableSummaryAsync(TaxReport report);
    Task<string> GenerateSavingsAdviceAsync(decimal taxLiability, decimal income);
    Task<string> GenerateSMERoadmapAsync(SMETaxCalculationResult result);
    Task<string> GenerateHumanReadableSummary(TaxReport report);
    Task<string> GenerateSavingsAdvice(decimal taxLiability, decimal income);
    Task<string> GenerateSMERoadmap(SMETaxCalculationResult result);
}

public interface IPdfService
{
    Task<byte[]> GenerateTaxReportPdfAsync(TaxReport report, string summary, string advice);
    Task<string> UploadPdfToBlobStorageAsync(byte[] pdfContent, string fileName);
}

public interface IPaymentService
{
    Task<PaymentInitiationResponse> InitiatePaymentAsync(PaymentRequest request);
    Task<PaymentVerificationResponse> VerifyPaymentAsync(string reference);
}

public interface IAuthenticationService
{
    Task<AuthenticationResponse> RegisterAsync(RegisterRequest request);
    Task<AuthenticationResponse> LoginAsync(LoginRequest request);
    Task<TokenRefreshResponse> RefreshTokenAsync(string refreshToken);
    string GenerateJwtToken(User user);
    string GenerateRefreshToken();
}

public interface IBlobStorageService
{
    Task<string> UploadFileAsync(byte[] fileContent, string fileName, string containerName = "tax-reports");
    Task<byte[]> DownloadFileAsync(string fileName, string containerName = "tax-reports");
    Task<bool> DeleteFileAsync(string fileName, string containerName = "tax-reports");
}

// DTOs
public class TaxCalculationResult
{
    public decimal TaxLiability { get; set; }
    public decimal AllowableDeductions { get; set; }
    public decimal ConsolidatedRelief { get; set; }
    public decimal ProjectedSavings { get; set; }
    public Dictionary<string, decimal> TaxBands { get; set; } = new();
}

public class SMETaxCalculationResult
{
    public bool VatEligible { get; set; }
    public decimal VatLiability { get; set; }
    public decimal WhtLiability { get; set; }
    public decimal TotalTaxObligation { get; set; }
    public string ComplianceRoadmap { get; set; } = string.Empty;
}

public class IndividualTaxInput
{
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

public class SMETaxInput
{
    public decimal AnnualTurnover { get; set; }
    public decimal OperatingExpenses { get; set; }
    public decimal PayrollCost { get; set; }
    public decimal Assets { get; set; }
    public decimal VatableIncome { get; set; }
    public decimal WhtExposure { get; set; }
}

public class PaymentInitiationResponse
{
    public string Reference { get; set; } = string.Empty;
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string AccessCode { get; set; } = string.Empty;
}

public class PaymentRequest
{
    public decimal Amount { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
}

public class PaymentVerificationResponse
{
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
}

public class AuthenticationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? JwtToken { get; set; }
    public string? RefreshToken { get; set; }
    public UserDto? User { get; set; }
    public AuthenticationData? Data { get; set; }
}

public class AuthenticationData
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? JwtToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; } = 3600;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
}

public class TokenRefreshResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TokenRefreshData? Data { get; set; }
}

public class TokenRefreshData
{
    public string JwtToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; } = 3600;
}

public class TokenRefreshResponseOld
{
    public string JwtToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string UserType { get; set; } = "Individual";
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
