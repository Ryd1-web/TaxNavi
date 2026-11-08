using Microsoft.Extensions.Logging;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Exceptions;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;

namespace TaxNavi.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPaymentRepository _payments;
    private readonly IUserRepository _users;
    private readonly IPaymentService _gateway;
    private readonly ITaxRuleProvider _rules;
    private readonly SubscriptionOptions _options;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        ISubscriptionRepository subscriptions,
        IPaymentRepository payments,
        IUserRepository users,
        IPaymentService gateway,
        ITaxRuleProvider rules,
        SubscriptionOptions options,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        ILogger<SubscriptionService> logger)
    {
        _subscriptions = subscriptions;
        _payments = payments;
        _users = users;
        _gateway = gateway;
        _rules = rules;
        _options = options;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public bool IsRecurring => !string.IsNullOrWhiteSpace(_options.SmePlanCode);
    public string? PlanCode => IsRecurring ? _options.SmePlanCode!.Trim() : null;
    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;
    private int PeriodDays => Math.Max(1, _rules.Monetization.SmeSubscriptionPeriodDays);

    public async Task<Subscription?> ActivateFromPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment.UserId == null)
        {
            _logger.LogWarning("Subscription payment {Reference} has no user; not activated", payment.Reference);
            return null;
        }

        var now = UtcNow;
        var current = await _subscriptions.GetLatestForUserAsync(payment.UserId.Value, cancellationToken);

        if (!string.IsNullOrEmpty(payment.PlanCode))
        {
            // Recurring: grant the first period now; Paystack's subscription.create / invoice events refine dates.
            if (current != null && current.IsRecurring && current.Status is not (SubscriptionStatuses.Cancelled or SubscriptionStatuses.Expired))
            {
                current.Status = SubscriptionStatuses.Active;
                current.CurrentPeriodEnd = Max(current.CurrentPeriodEnd, now.AddDays(PeriodDays));
                current.LastPaymentId = payment.Id;
                current.Amount = payment.Amount;
                current.Currency = payment.Currency;
                current.UpdatedAt = now;
                await _subscriptions.UpdateAsync(current, cancellationToken);
                return current;
            }

            var created = new Subscription
            {
                Id = Guid.NewGuid(),
                UserId = payment.UserId.Value,
                Plan = "SmeMonthly",
                Status = SubscriptionStatuses.Active,
                IsRecurring = true,
                PlanCode = payment.PlanCode,
                Amount = payment.Amount,
                Currency = payment.Currency,
                CurrentPeriodStart = now,
                CurrentPeriodEnd = now.AddDays(PeriodDays),
                LastPaymentId = payment.Id,
                CreatedAt = now
            };
            await _subscriptions.AddAsync(created, cancellationToken);
            _logger.LogInformation("Recurring SME plan activated for payment {Reference}", payment.Reference);
            return created;
        }

        // One-off monthly pass: extend from the later of now or the current paid end.
        if (current != null && !current.IsRecurring && current.IsActiveAt(now))
        {
            current.CurrentPeriodEnd = current.CurrentPeriodEnd.AddDays(PeriodDays);
            current.LastPaymentId = payment.Id;
            current.Amount = payment.Amount;
            current.Currency = payment.Currency;
            current.UpdatedAt = now;
            await _subscriptions.UpdateAsync(current, cancellationToken);
            return current;
        }

        var pass = new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = payment.UserId.Value,
            Plan = "SmeMonthly",
            Status = SubscriptionStatuses.Active,
            IsRecurring = false,
            Amount = payment.Amount,
            Currency = payment.Currency,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.AddDays(PeriodDays),
            LastPaymentId = payment.Id,
            CreatedAt = now
        };
        await _subscriptions.AddAsync(pass, cancellationToken);
        return pass;
    }

    public async Task SyncAsync(string subscriptionCode, string? customerEmailHint)
    {
        var remote = await _gateway.FetchSubscriptionAsync(subscriptionCode);
        if (remote == null)
        {
            _logger.LogWarning("Could not fetch subscription {SubscriptionCode} from Paystack", subscriptionCode);
            return;
        }

        var now = UtcNow;
        var local = await _subscriptions.GetByProviderCodeAsync(subscriptionCode);
        var isNew = false;

        if (local == null)
        {
            var email = remote.CustomerEmail ?? customerEmailHint;
            var user = string.IsNullOrWhiteSpace(email) ? null : await _users.GetByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("Subscription {SubscriptionCode} belongs to an unknown customer; skipped", subscriptionCode);
                return;
            }

            var latest = await _subscriptions.GetLatestForUserAsync(user.Id);
            if (latest != null && latest.IsRecurring && string.IsNullOrEmpty(latest.ProviderSubscriptionCode))
            {
                local = latest;
            }
            else
            {
                isNew = true;
                local = new Subscription
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Plan = "SmeMonthly",
                    IsRecurring = true,
                    CurrentPeriodStart = now,
                    CurrentPeriodEnd = now,
                    CreatedAt = now
                };
            }
        }

        local.ProviderSubscriptionCode = remote.SubscriptionCode is { Length: > 0 } code ? code : subscriptionCode;
        local.ProviderEmailToken = remote.EmailToken ?? local.ProviderEmailToken;
        local.ProviderCustomerCode = remote.CustomerCode ?? local.ProviderCustomerCode;
        local.PlanCode = remote.PlanCode ?? local.PlanCode;
        if (remote.AmountKobo > 0)
        {
            local.Amount = Money.FromKobo(remote.AmountKobo);
        }
        local.IsRecurring = true;
        local.Status = MapStatus(remote.Status, local.Status);
        if (local.Status is SubscriptionStatuses.NonRenewing or SubscriptionStatuses.Cancelled)
        {
            local.CancelledAt ??= now;
        }

        if (remote.NextPaymentDate.HasValue)
        {
            local.NextPaymentDate = remote.NextPaymentDate;
            if (local.Status is SubscriptionStatuses.Active or SubscriptionStatuses.NonRenewing or SubscriptionStatuses.PastDue)
            {
                // The paid period runs until the next billing date.
                local.CurrentPeriodEnd = Max(local.CurrentPeriodEnd, remote.NextPaymentDate.Value);
            }
        }

        local.UpdatedAt = now;
        if (isNew)
        {
            await _subscriptions.AddAsync(local);
        }
        else
        {
            await _subscriptions.UpdateAsync(local);
        }

        _logger.LogInformation("Subscription {SubscriptionCode} synced: {Status}", local.ProviderSubscriptionCode, local.Status);
    }

    public async Task MarkRenewalFailedAsync(string subscriptionCode)
    {
        var local = await _subscriptions.GetByProviderCodeAsync(subscriptionCode);
        if (local == null)
        {
            return;
        }

        // Record when the renewal failed. The status itself comes from Paystack's subscription state
        // ("attention" -> PastDue) via the SyncAsync call that follows; it is not assumed here.
        local.LastPaymentFailedAt = UtcNow;
        local.UpdatedAt = UtcNow;
        await _subscriptions.UpdateAsync(local);
        _logger.LogWarning("Renewal failed for subscription {SubscriptionCode}", subscriptionCode);
    }

    public async Task RecordRenewalChargeAsync(string reference)
    {
        if (await _payments.GetByReferenceAsync(reference) != null)
        {
            return; // already recorded
        }

        // Never trust the webhook body for money: confirm the charge with Paystack.
        var verification = await _gateway.VerifyPaymentAsync(reference);
        if (!verification.Success || !string.Equals(verification.Reference, reference, StringComparison.Ordinal))
        {
            _logger.LogWarning("Renewal charge {Reference} could not be verified", reference);
            return;
        }

        var user = string.IsNullOrWhiteSpace(verification.CustomerEmail) ? null : await _users.GetByEmailAsync(verification.CustomerEmail);
        if (user == null)
        {
            _logger.LogWarning("Renewal charge {Reference} belongs to an unknown customer", reference);
            return;
        }

        var now = UtcNow;
        var subscription = await _subscriptions.GetLatestForUserAsync(user.Id);
        try
        {
            // The renewal payment and the subscription update commit together.
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    SubscriptionId = subscription?.Id,
                    Purpose = PaymentPurposes.SmeSubscription,
                    Amount = Money.FromKobo(verification.AmountKobo),
                    Currency = verification.Currency,
                    Status = PaymentStatuses.Completed,
                    Reference = reference,
                    Email = user.Email,
                    PlanCode = verification.PlanCode,
                    ProviderTransactionId = verification.TransactionId,
                    Channel = verification.Channel,
                    CreatedAt = now,
                    PaidAt = verification.PaidAt ?? now,
                    VerifiedAt = now,
                    FulfilledAt = now
                };
                await _payments.AddAsync(payment, ct);

                if (subscription != null && subscription.IsRecurring)
                {
                    subscription.LastPaymentId = payment.Id;
                    subscription.LastPaymentFailedAt = null;
                    subscription.Amount = payment.Amount;
                    subscription.Currency = payment.Currency;
                    if (string.IsNullOrEmpty(subscription.ProviderSubscriptionCode))
                    {
                        // No Paystack subscription linked yet: provisional period until subscription.create arrives.
                        subscription.Status = SubscriptionStatuses.Active;
                        subscription.CurrentPeriodEnd = Max(subscription.CurrentPeriodEnd, now.AddDays(PeriodDays));
                    }
                    subscription.UpdatedAt = now;
                    await _subscriptions.UpdateAsync(subscription, ct);
                }
            });
        }
        catch (DuplicateRecordException)
        {
            return; // a concurrent delivery recorded this renewal first
        }

        // Status and the next billing date come from Paystack itself (external call, after the commit).
        if (subscription?.IsRecurring == true && !string.IsNullOrEmpty(subscription.ProviderSubscriptionCode))
        {
            await SyncAsync(subscription.ProviderSubscriptionCode, user.Email);
        }

        _logger.LogInformation("Renewal charge {Reference} recorded", reference);
    }

    public async Task<BillingResult<bool>> CancelAsync(Guid userId)
    {
        var subscription = await _subscriptions.GetLatestForUserAsync(userId);
        if (subscription == null || !subscription.IsRecurring ||
            string.IsNullOrEmpty(subscription.ProviderSubscriptionCode) || string.IsNullOrEmpty(subscription.ProviderEmailToken))
        {
            return BillingResult<bool>.Fail(BillingOutcome.NotFound, "You don't have a recurring plan to cancel.");
        }

        if (subscription.Status is SubscriptionStatuses.NonRenewing or SubscriptionStatuses.Cancelled or SubscriptionStatuses.Expired)
        {
            return BillingResult<bool>.Ok(true, "Your plan is already set not to renew.");
        }

        if (!await _gateway.DisableSubscriptionAsync(subscription.ProviderSubscriptionCode, subscription.ProviderEmailToken))
        {
            return BillingResult<bool>.Fail(BillingOutcome.ProviderError, "We couldn't cancel the plan right now. Please try again.");
        }

        // Paystack stops future charges; access continues until the end of the paid period.
        subscription.Status = SubscriptionStatuses.NonRenewing;
        subscription.CancelledAt = UtcNow;
        subscription.UpdatedAt = UtcNow;
        await _subscriptions.UpdateAsync(subscription);
        await SyncAsync(subscription.ProviderSubscriptionCode, null);

        _logger.LogInformation("Subscription {SubscriptionCode} cancelled by customer", subscription.ProviderSubscriptionCode);
        return BillingResult<bool>.Ok(true, "Your plan won't renew. You keep access until the end of the current period.");
    }

    public async Task<BillingResult<string>> GetManageLinkAsync(Guid userId)
    {
        var subscription = await _subscriptions.GetLatestForUserAsync(userId);
        if (subscription?.IsRecurring != true || string.IsNullOrEmpty(subscription.ProviderSubscriptionCode))
        {
            return BillingResult<string>.Fail(BillingOutcome.NotFound, "You don't have a recurring plan.");
        }

        var link = await _gateway.GetSubscriptionManageLinkAsync(subscription.ProviderSubscriptionCode);
        return string.IsNullOrEmpty(link)
            ? BillingResult<string>.Fail(BillingOutcome.ProviderError, "We couldn't open the card update page. Please try again.")
            : BillingResult<string>.Ok(link);
    }

    /// <summary>Paystack statuses: active, non-renewing, attention (charge failed), completed, cancelled.</summary>
    public static string MapStatus(string? providerStatus, string fallback) => providerStatus?.ToLowerInvariant() switch
    {
        "active" => SubscriptionStatuses.Active,
        "non-renewing" => SubscriptionStatuses.NonRenewing,
        "attention" => SubscriptionStatuses.PastDue,
        "completed" => SubscriptionStatuses.Expired,
        "cancelled" => SubscriptionStatuses.Cancelled,
        _ => fallback
    };

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
