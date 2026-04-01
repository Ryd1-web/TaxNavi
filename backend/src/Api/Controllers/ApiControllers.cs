namespace TaxNavi.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Application.DTOs;
using FluentValidation;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthController(
        IAuthenticationService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var validationResult = await _registerValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList() });
        }

        var result = await _authService.RegisterAsync(request);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var validationResult = await _loginValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList() });
        }

        var result = await _authService.LoginAsync(request);
        if (!result.Success)
            return Unauthorized(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
    {
        var result = await _authService.RefreshTokenAsync(refreshToken);
        return Ok(result);
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TaxAssessmentController : ControllerBase
{
    private readonly ITaxCalculationService _taxCalculationService;
    private readonly IAiTaxAdvisorService _aiService;
    private readonly IPdfService _pdfService;
    private readonly ITaxReportRepository _reportRepository;
    private readonly ITaxProfileRepository _profileRepository;
    private readonly IValidator<IndividualTaxAssessmentDto> _individualValidator;
    private readonly IValidator<SMEAssessmentDto> _smeValidator;

    public TaxAssessmentController(
        ITaxCalculationService taxCalculationService,
        IAiTaxAdvisorService aiService,
        IPdfService pdfService,
        ITaxReportRepository reportRepository,
        ITaxProfileRepository profileRepository,
        IValidator<IndividualTaxAssessmentDto> individualValidator,
        IValidator<SMEAssessmentDto> smeValidator)
    {
        _taxCalculationService = taxCalculationService;
        _aiService = aiService;
        _pdfService = pdfService;
        _reportRepository = reportRepository;
        _profileRepository = profileRepository;
        _individualValidator = individualValidator;
        _smeValidator = smeValidator;
    }

    [HttpPost("individual")]
    public async Task<IActionResult> AssessIndividualTax([FromBody] IndividualTaxAssessmentDto request)
    {
        var userId = GetUserId();
        request.UserId = userId;

        var validationResult = await _individualValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList() });
        }

        var input = new IndividualTaxInput
        {
            MonthlySalary = request.MonthlySalary,
            AnnualBonus = request.AnnualBonus,
            Allowances = request.Allowances,
            BusinessIncome = request.BusinessIncome,
            FreelanceIncome = request.FreelanceIncome,
            InvestmentIncome = request.InvestmentIncome,
            RentPaid = request.RentPaid,
            OtherExpenses = request.OtherExpenses
        };

        var taxResult = await _taxCalculationService.CalculateIndividualTaxAsync(input);
        var summary = await _aiService.GenerateHumanReadableSummaryAsync(new Domain.Entities.TaxReport
        {
            TotalIncome = input.MonthlySalary * 12 + input.AnnualBonus + input.Allowances + input.BusinessIncome + input.FreelanceIncome + input.InvestmentIncome,
            AllowableDeductions = input.RentPaid + input.OtherExpenses,
            TaxLiability = taxResult.TaxLiability,
            ProjectedSavings = taxResult.ProjectedSavings,
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            UserId = userId,
            TaxProfileId = Guid.NewGuid()
        });

        var advice = await _aiService.GenerateSavingsAdviceAsync(taxResult.TaxLiability, input.MonthlySalary * 12);

        return Ok(new
        {
            success = true,
            data = new
            {
                taxLiability = taxResult.TaxLiability,
                totalIncome = input.MonthlySalary * 12 + input.AnnualBonus + input.Allowances + input.BusinessIncome + input.FreelanceIncome + input.InvestmentIncome,
                allowableDeductions = taxResult.AllowableDeductions,
                consolidatedRelief = taxResult.ConsolidatedRelief,
                projectedSavings = taxResult.ProjectedSavings,
                taxBands = taxResult.TaxBands,
                aiSummary = summary,
                savingsAdvice = advice
            }
        });
    }

    [HttpPost("sme")]
    public async Task<IActionResult> AssessSMETax([FromBody] SMEAssessmentDto request)
    {
        var userId = GetUserId();
        request.UserId = userId;

        var validationResult = await _smeValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList() });
        }

        var input = new SMETaxInput
        {
            AnnualTurnover = request.AnnualTurnover,
            OperatingExpenses = request.OperatingExpenses,
            PayrollCost = request.PayrollCost,
            Assets = request.Assets,
            VatableIncome = request.VatableIncome
        };

        var taxResult = await _taxCalculationService.CalculateSMETaxAsync(input);
        var roadmap = await _aiService.GenerateSMERoadmapAsync(taxResult);

        return Ok(new
        {
            success = true,
            data = new
            {
                vatEligible = taxResult.VatEligible,
                vatLiability = taxResult.VatLiability,
                whtLiability = taxResult.WhtLiability,
                totalTaxObligation = taxResult.TotalTaxObligation,
                complianceRoadmap = roadmap
            }
        });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ITaxReportRepository _reportRepository;

    public ReportsController(ITaxReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserReports()
    {
        var userId = GetUserId();
        var reports = await _reportRepository.GetByUserIdAsync(userId);

        var reportDtos = reports.Select(r => new TaxReportDto
        {
            Id = r.Id,
            UserId = r.UserId,
            ReportType = r.ReportType,
            TaxLiability = r.TaxLiability,
            TotalIncome = r.TotalIncome,
            AllowableDeductions = r.AllowableDeductions,
            ProjectedSavings = r.ProjectedSavings,
            PdfUrl = r.PdfUrl,
            CreatedAt = r.CreatedAt
        }).ToList();

        return Ok(new { success = true, data = reportDtos });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetReport(Guid id)
    {
        var userId = GetUserId();
        var report = await _reportRepository.GetByIdAsync(id);

        if (report == null || report.UserId != userId)
            return NotFound(new { success = false, message = "Report not found" });

        var reportDto = new TaxReportDto
        {
            Id = report.Id,
            UserId = report.UserId,
            ReportType = report.ReportType,
            TaxLiability = report.TaxLiability,
            TotalIncome = report.TotalIncome,
            AllowableDeductions = report.AllowableDeductions,
            ProjectedSavings = report.ProjectedSavings,
            PdfUrl = report.PdfUrl,
            CreatedAt = report.CreatedAt
        };

        return Ok(new { success = true, data = reportDto });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentRepository _paymentRepository;

    public PaymentsController(IPaymentService paymentService, IPaymentRepository paymentRepository)
    {
        _paymentService = paymentService;
        _paymentRepository = paymentRepository;
    }

    [HttpPost("initiate")]
    public async Task<IActionResult> InitiatePayment([FromBody] PaymentInitiationDto request)
    {
        var userId = GetUserId();
        var paymentRequest = new PaymentRequest
        {
            Amount = request.Amount,
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
            Reference = $"PAY-{Guid.NewGuid()}"
        };

        var response = await _paymentService.InitiatePaymentAsync(paymentRequest);
        return Ok(new { success = true, data = response });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPayment([FromBody] PaymentVerificationDto request)
    {
        var response = await _paymentService.VerifyPaymentAsync(request.Reference);
        return Ok(new { success = true, data = response });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
    }
}
