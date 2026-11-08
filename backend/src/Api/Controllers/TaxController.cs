using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;

namespace TaxNavi.Api.Controllers;

[Route("api/tax")]
[EnableRateLimiting("calculate")]
public class TaxController : ApiControllerBase
{
    private readonly ITaxCalculationEngine _engine;
    private readonly ITaxAccessControlService _accessControl;
    private readonly ITaxRuleProvider _rules;
    private readonly IBillingService _billing;
    private readonly ITaxAssistantService _assistant;
    private readonly ITaxReportService _reportService;
    private readonly ILogger<TaxController> _logger;

    public TaxController(
        ITaxCalculationEngine engine,
        ITaxAccessControlService accessControl,
        ITaxRuleProvider rules,
        IBillingService billing,
        ITaxAssistantService assistant,
        ITaxReportService reportService,
        ILogger<TaxController> logger)
    {
        _engine = engine;
        _accessControl = accessControl;
        _rules = rules;
        _billing = billing;
        _assistant = assistant;
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>Public, versioned rule set for a tax year: bands, thresholds, deadlines, pricing.</summary>
    [AllowAnonymous]
    [HttpGet("rules")]
    [DisableRateLimiting] // static, cheap and loaded on every page; many users share carrier IPs (CGNAT)
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public IActionResult GetRules([FromQuery] int? year)
    {
        var taxYear = year ?? _rules.DefaultTaxYear;
        var set = _rules.FindRuleSet(taxYear);
        if (set == null)
        {
            return Failure(StatusCodes.Status400BadRequest, $"Tax year {taxYear} is not supported.", "UNSUPPORTED_TAX_YEAR");
        }

        decimal lower = 0m;
        var bands = new List<TaxBandChargeDto>();
        foreach (var band in set.PersonalIncomeTax.Bands.OrderBy(b => b.UpperLimit ?? decimal.MaxValue))
        {
            bands.Add(new TaxBandChargeDto
            {
                Label = NigerianTaxCalculationEngine.BuildBandLabel(lower, band.UpperLimit, band.Rate),
                LowerLimit = lower,
                UpperLimit = band.UpperLimit,
                Rate = band.Rate
            });
            lower = band.UpperLimit ?? lower;
        }

        return Success(new TaxRulesPublicDto
        {
            DefaultTaxYear = _rules.DefaultTaxYear,
            SupportedTaxYears = _rules.SupportedTaxYears.ToList(),
            TaxYear = taxYear,
            RuleSet = NigerianTaxCalculationEngine.ToSummary(set),
            ReliefModel = set.PersonalIncomeTax.ReliefModel,
            RentReliefRate = set.PersonalIncomeTax.RentReliefRate,
            RentReliefCap = set.PersonalIncomeTax.RentReliefCap,
            Bands = bands,
            Vat = set.Vat,
            CompanyIncomeTax = set.CompanyIncomeTax,
            Notes = set.Notes,
            Deadlines = set.Deadlines,
            Monetization = _rules.Monetization
        });
    }

    /// <summary>Anonymous calculation. Returns the free summary only and stores nothing.</summary>
    [AllowAnonymous]
    [HttpPost("calculate/free")]
    public IActionResult CalculateFree([FromBody] TaxCalculationRequestDto request)
    {
        var report = _engine.GenerateFullReport(request);
        return Success(_accessControl.BuildResponse(report, unlockedVia: null, _billing.PaymentsEnabled));
    }

    /// <summary>Kept for API compatibility; client-supplied access flags are ignored.</summary>
    [Authorize]
    [HttpPost("calculate/full")]
    public Task<IActionResult> CalculateFull([FromBody] FullTaxCalculationRequestDto request) => SaveReport(request.Input);

    /// <summary>Calculates and saves a report to the user's account; premium content is returned only if entitled.</summary>
    [Authorize]
    [HttpPost("reports")]
    public async Task<IActionResult> SaveReport([FromBody] TaxCalculationRequestDto request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (entity, report, _) = await _reportService.CreateAsync(request, userId);
        _logger.LogInformation("Saved tax report {ReportId}", entity.Id);

        var unlockedVia = await _billing.ResolveReportAccessAsync(userId, entity);
        return Success(_accessControl.BuildResponse(report, unlockedVia, _billing.PaymentsEnabled, entity.Id));
    }

    /// <summary>Grounded assistant: answers only from the active rule set and the engine's output.</summary>
    [AllowAnonymous]
    [HttpPost("assistant")]
    public IActionResult Ask([FromBody] AssistantQuestionDto question)
    {
        if (string.IsNullOrWhiteSpace(question.Question) || question.Question.Length > 500)
        {
            return Failure(StatusCodes.Status400BadRequest, "Ask a question of up to 500 characters.");
        }

        return Success(_assistant.Answer(question));
    }
}
