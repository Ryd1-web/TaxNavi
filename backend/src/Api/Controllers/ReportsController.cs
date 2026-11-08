using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using TaxNavi.Domain.Interfaces;

namespace TaxNavi.Api.Controllers;

/// <summary>Saved reports. Every action is scoped to the authenticated user.</summary>
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ApiControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ITaxReportRepository _reports;
    private readonly IUserRepository _users;
    private readonly IPdfService _pdf;
    private readonly IBillingService _billing;
    private readonly ITaxAccessControlService _accessControl;
    private readonly ITaxReportService _reportService;

    public ReportsController(
        ITaxReportRepository reports,
        IUserRepository users,
        IPdfService pdf,
        IBillingService billing,
        ITaxAccessControlService accessControl,
        ITaxReportService reportService)
    {
        _reports = reports;
        _users = users;
        _pdf = pdf;
        _billing = billing;
        _accessControl = accessControl;
        _reportService = reportService;
    }

    [HttpGet]
    [HttpGet("user")]
    public async Task<IActionResult> List()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var reports = await _reports.GetByUserIdAsync(userId);
        return Success(reports.Select(r => new TaxReportListItemDto
        {
            Id = r.Id,
            ReportType = r.ReportType,
            TaxYear = r.TaxYear,
            RuleSetId = r.RuleSetId,
            TotalIncome = r.TotalIncome,
            AllowableDeductions = r.AllowableDeductions,
            TaxLiability = r.TaxLiability,
            IsUnlocked = r.IsUnlocked,
            CreatedAt = r.CreatedAt
        }).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var entity = await _reports.GetForUserAsync(id, userId);
        var snapshot = entity == null ? null : Deserialize(entity.JsonData);
        if (entity == null || snapshot == null)
        {
            return Failure(StatusCodes.Status404NotFound, "Report not found.");
        }

        var unlockedVia = await _billing.ResolveReportAccessAsync(userId, entity);
        var response = _accessControl.BuildResponse(snapshot, unlockedVia, _billing.PaymentsEnabled, entity.Id);
        return Success(new { createdAt = entity.CreatedAt, report = response });
    }

    [HttpGet("{id:guid}/pdf")]
    [HttpGet("download/{id:guid}")]
    public async Task<IActionResult> DownloadPdf(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var entity = await _reports.GetForUserAsync(id, userId);
        if (entity == null)
        {
            return Failure(StatusCodes.Status404NotFound, "Report not found.");
        }

        if (await _billing.ResolveReportAccessAsync(userId, entity) == null)
        {
            return Failure(StatusCodes.Status402PaymentRequired, "Unlock this report to download the PDF.", "PREMIUM_REQUIRED");
        }

        var user = await _users.GetByIdAsync(userId);
        var name = user == null ? "Taxpayer" : $"{user.FirstName} {user.LastName}".Trim();
        var bytes = await _pdf.GenerateTaxReportPdfAsync(entity, entity.Summary, string.Empty, name);
        return File(bytes, "application/pdf", $"taxnavi-report-{entity.TaxYear}-{entity.Id.ToString()[..8]}.pdf");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var entity = await _reports.GetForUserAsync(id, userId);
        if (entity == null)
        {
            return Failure(StatusCodes.Status404NotFound, "Report not found.");
        }

        await _reportService.DeleteAsync(entity, HttpContext.RequestAborted);
        return Success(null, "Report deleted");
    }

    // -----------------------------------------------------------------------
    // Guest reports: accessed with the X-Report-Access-Token issued at guest checkout.
    // -----------------------------------------------------------------------

    private const string AccessTokenHeader = "X-Report-Access-Token";

    [HttpGet("guest/{id:guid}")]
    [AllowAnonymous]
    [EnableRateLimiting("calculate")]
    public async Task<IActionResult> GetGuest(Guid id)
    {
        var token = Request.Headers[AccessTokenHeader].ToString();
        var entity = await _reports.GetByIdAsync(id);
        var snapshot = entity == null ? null : Deserialize(entity.JsonData);
        if (entity == null || snapshot == null || !_billing.CanView(CurrentUserId(), entity, token))
        {
            return Failure(StatusCodes.Status404NotFound, "Report not found.");
        }

        var unlockedVia = await _billing.ResolveReportAccessAsync(CurrentUserId(), entity, token);
        var response = _accessControl.BuildResponse(snapshot, unlockedVia, _billing.PaymentsEnabled, entity.Id);
        return Success(new { createdAt = entity.CreatedAt, isGuest = entity.UserId == null, report = response });
    }

    [HttpGet("guest/{id:guid}/pdf")]
    [AllowAnonymous]
    [EnableRateLimiting("calculate")]
    public async Task<IActionResult> GuestPdf(Guid id)
    {
        var token = Request.Headers[AccessTokenHeader].ToString();
        var entity = await _reports.GetByIdAsync(id);
        if (entity == null || !_billing.CanView(CurrentUserId(), entity, token))
        {
            return Failure(StatusCodes.Status404NotFound, "Report not found.");
        }

        if (await _billing.ResolveReportAccessAsync(CurrentUserId(), entity, token) == null)
        {
            return Failure(StatusCodes.Status402PaymentRequired, "Unlock this report to download the PDF.", "PREMIUM_REQUIRED");
        }

        var bytes = await _pdf.GenerateTaxReportPdfAsync(entity, entity.Summary, string.Empty, "Taxpayer");
        return File(bytes, "application/pdf", $"taxnavi-report-{entity.TaxYear}-{entity.Id.ToString()[..8]}.pdf");
    }

    /// <summary>Moves a guest report (and its payments) into the signed-in account.</summary>
    [HttpPost("{id:guid}/claim")]
    public async Task<IActionResult> Claim(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _billing.ClaimGuestReportAsync(userId, id, Request.Headers[AccessTokenHeader].ToString());
        return PaymentsController.Map(this, result);
    }

    private Guid? CurrentUserId() => TryGetUserId(out var id) ? id : null;

    private static FullTaxReportDto? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<FullTaxReportDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
