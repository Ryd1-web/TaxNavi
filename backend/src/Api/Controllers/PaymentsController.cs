using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using TaxNavi.Domain.Interfaces;

namespace TaxNavi.Api.Controllers;

[Route("api/[controller]")]
public class PaymentsController : ApiControllerBase
{
    private readonly IBillingService _billing;
    private readonly IPaymentService _gateway;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IBillingService billing, IPaymentService gateway, IConfiguration configuration, ILogger<PaymentsController> logger)
    {
        _billing = billing;
        _gateway = gateway;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Starts a checkout for a premium report (guests welcome: supply an email and the calculation or a guest report
    /// + access token) or for the SME plan (signed-in). Prices and the customer email are resolved server-side.
    /// </summary>
    [HttpPost("checkout")]
    [HttpPost("initiate")]
    [AllowAnonymous]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto request)
    {
        Guid? userId = TryGetUserId(out var id) ? id : null;
        var result = await _billing.CreateCheckoutAsync(userId, request, CheckoutUrls(_configuration));
        return ToResponse(result);
    }

    /// <summary>Public payment status for the UI. Exposes flags only — never keys.</summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [DisableRateLimiting]
    public IActionResult Config() => Success(new { enabled = _billing.PaymentsEnabled, testMode = _billing.TestMode, currency = "NGN", provider = "Paystack" });

    /// <summary>Re-verifies a transaction with Paystack and fulfils it if paid. Safe to call repeatedly.</summary>
    [HttpPost("verify")]
    [AllowAnonymous]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Verify([FromBody] VerifyPaymentDto request)
    {
        Guid? userId = TryGetUserId(out var id) ? id : null;
        return ToResponse(await _billing.VerifyAsync(userId, request.Reference));
    }

    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> History()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Success(await _billing.GetPaymentHistoryAsync(userId));
    }

    /// <summary>
    /// Paystack webhook. The HMAC-SHA512 signature is checked over the raw body before anything is parsed;
    /// processing is idempotent, so Paystack retries are harmless.
    /// </summary>
    [HttpPost("webhook/paystack")]
    [AllowAnonymous]
    [DisableRateLimiting]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> PaystackWebhook(CancellationToken cancellationToken)
    {
        byte[] raw;
        using (var buffer = new MemoryStream())
        {
            await Request.Body.CopyToAsync(buffer, cancellationToken);
            raw = buffer.ToArray();
        }

        if (!_gateway.IsValidWebhookSignature(raw, Request.Headers["x-paystack-signature"]))
        {
            _logger.LogWarning("Rejected Paystack webhook with a missing or invalid signature");
            return Unauthorized();
        }

        try
        {
            var result = await _billing.HandleWebhookAsync(Encoding.UTF8.GetString(raw));
            return Ok(new { received = true, result.EventType, result.Outcome });
        }
        catch (JsonException)
        {
            return BadRequest();
        }
        catch (Exception ex)
        {
            // Non-2xx makes Paystack retry later; the idempotency record makes the retry safe.
            _logger.LogError(ex, "Paystack webhook processing failed");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    public static CheckoutUrls CheckoutUrls(IConfiguration configuration)
    {
        var frontend = (configuration["App:FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');
        return new CheckoutUrls($"{frontend}/checkout/callback", $"{frontend}/checkout/callback");
    }

    public static IActionResult Map<T>(ControllerBase controller, BillingResult<T> result)
    {
        object Body(string? code) => new { success = result.Outcome == BillingOutcome.Ok, message = result.Message, code, data = result.Data };
        return result.Outcome switch
        {
            BillingOutcome.Ok => controller.Ok(Body(null)),
            BillingOutcome.NotFound => controller.NotFound(Body("NOT_FOUND")),
            BillingOutcome.Invalid => controller.BadRequest(Body("INVALID")),
            BillingOutcome.Unauthorized => controller.Unauthorized(Body("UNAUTHORIZED")),
            BillingOutcome.AlreadyEntitled => controller.Conflict(Body("ALREADY_UNLOCKED")),
            BillingOutcome.PaymentsNotConfigured => controller.StatusCode(StatusCodes.Status503ServiceUnavailable, Body("PAYMENTS_NOT_CONFIGURED")),
            BillingOutcome.ProviderError => controller.StatusCode(StatusCodes.Status502BadGateway, Body("PROVIDER_ERROR")),
            BillingOutcome.Pending => controller.StatusCode(StatusCodes.Status202Accepted, Body("PENDING")),
            BillingOutcome.Cancelled => controller.StatusCode(StatusCodes.Status402PaymentRequired, Body("PAYMENT_CANCELLED")),
            _ => controller.StatusCode(StatusCodes.Status402PaymentRequired, Body("PAYMENT_FAILED"))
        };
    }

    private IActionResult ToResponse<T>(BillingResult<T> result) => Map(this, result);
}
