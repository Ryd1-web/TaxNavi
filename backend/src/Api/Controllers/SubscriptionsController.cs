using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using TaxNavi.Domain.Entities;

namespace TaxNavi.Api.Controllers;

/// <summary>SME plan management. Entitlement is always decided server-side from the stored subscription.</summary>
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController : ApiControllerBase
{
    private readonly IBillingService _billing;
    private readonly ISubscriptionService _subscriptions;
    private readonly IConfiguration _configuration;

    public SubscriptionsController(IBillingService billing, ISubscriptionService subscriptions, IConfiguration configuration)
    {
        _billing = billing;
        _subscriptions = subscriptions;
        _configuration = configuration;
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Success(await _billing.GetEntitlementsAsync(userId));
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _billing.CreateCheckoutAsync(userId, new CheckoutRequestDto { Purpose = PaymentPurposes.SmeSubscription },
            PaymentsController.CheckoutUrls(_configuration));
        return PaymentsController.Map(this, result);
    }

    /// <summary>Stops future renewals at Paystack; access continues to the end of the paid period.</summary>
    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return PaymentsController.Map(this, await _subscriptions.CancelAsync(userId));
    }

    /// <summary>Paystack-hosted page where the customer can update their card.</summary>
    [HttpGet("manage-link")]
    public async Task<IActionResult> ManageLink()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return PaymentsController.Map(this, await _subscriptions.GetManageLinkAsync(userId));
    }
}
