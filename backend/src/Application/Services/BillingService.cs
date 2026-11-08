using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Exceptions;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;

namespace TaxNavi.Application.Services;

public class BillingService : IBillingService
{
    private readonly IUserRepository _users;
    private readonly ITaxReportRepository _reports;
    private readonly IPaymentRepository _payments;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPaymentWebhookEventRepository _webhookEvents;
    private readonly IPaymentService _gateway;
    private readonly ITaxRuleProvider _rules;
    private readonly ITaxReportService _reportService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        IUserRepository users,
        ITaxReportRepository reports,
        IPaymentRepository payments,
        ISubscriptionRepository subscriptions,
        IPaymentWebhookEventRepository webhookEvents,
        IPaymentService gateway,
        ITaxRuleProvider rules,
        ITaxReportService reportService,
        ISubscriptionService subscriptionService,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        ILogger<BillingService> logger)
    {
        _users = users;
        _reports = reports;
        _payments = payments;
        _subscriptions = subscriptions;
        _webhookEvents = webhookEvents;
        _gateway = gateway;
        _rules = rules;
        _reportService = reportService;
        _subscriptionService = subscriptionService;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public bool PaymentsEnabled => _gateway.IsConfigured;
    public bool TestMode => _gateway.IsTestMode;
    private MonetizationRules Pricing => _rules.Monetization;
    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    public decimal ReportPriceFor(string reportType) => Pricing.PriceFor(reportType);

    // -----------------------------------------------------------------------
    // Entitlements
    // -----------------------------------------------------------------------

    public bool CanView(Guid? userId, TaxReport report, string? accessToken) =>
        (userId.HasValue && report.UserId == userId) ||
        (report.UserId == null && ReportAccessTokens.Matches(accessToken, report.AccessTokenHash));

    public async Task<string?> ResolveReportAccessAsync(Guid? userId, TaxReport report, string? accessToken = null)
    {
        if (userId.HasValue && report.UserId == userId)
        {
            if (report.IsUnlocked)
            {
                return UnlockSources.Purchase;
            }

            var subscription = await _subscriptions.GetLatestForUserAsync(userId.Value);
            return subscription?.IsActiveAt(UtcNow, Pricing.SubscriptionGraceDays) == true ? UnlockSources.Subscription : null;
        }

        if (report.UserId == null && ReportAccessTokens.Matches(accessToken, report.AccessTokenHash))
        {
            return report.IsUnlocked ? UnlockSources.Purchase : null;
        }

        return null;
    }

    public async Task<EntitlementsDto> GetEntitlementsAsync(Guid userId)
    {
        var subscription = await _subscriptions.GetLatestForUserAsync(userId);
        var reports = await _reports.GetByUserIdAsync(userId);
        var active = subscription?.IsActiveAt(UtcNow, Pricing.SubscriptionGraceDays) == true;

        return new EntitlementsDto
        {
            PaymentsEnabled = PaymentsEnabled,
            TestMode = TestMode,
            SubscriptionMode = _subscriptionService.IsRecurring ? "Recurring" : "OneTime",
            HasActiveSubscription = active,
            SubscriptionPlan = subscription?.Plan,
            SubscriptionStatus = subscription?.Status,
            SubscriptionRecurring = subscription?.IsRecurring == true,
            SubscriptionPeriodEnd = subscription?.CurrentPeriodEnd,
            NextPaymentDate = subscription?.NextPaymentDate,
            CanCancelSubscription = active && subscription!.IsRecurring &&
                                    subscription.Status is SubscriptionStatuses.Active or SubscriptionStatuses.PastDue &&
                                    !string.IsNullOrEmpty(subscription.ProviderSubscriptionCode),
            UnlockedReportCount = reports.Count(r => r.IsUnlocked),
            Pricing = Pricing
        };
    }

    // -----------------------------------------------------------------------
    // Checkout
    // -----------------------------------------------------------------------

    public async Task<BillingResult<CheckoutResponseDto>> CreateCheckoutAsync(Guid? userId, CheckoutRequestDto request, CheckoutUrls urls)
    {
        User? user = null;
        if (userId.HasValue)
        {
            user = await _users.GetByIdAsync(userId.Value);
            if (user == null || !user.IsActive)
            {
                return BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.Unauthorized, "Please sign in again.");
            }
        }

        return request.Purpose switch
        {
            PaymentPurposes.PremiumReport => await CreateReportCheckoutAsync(user, request, urls),
            PaymentPurposes.SmeSubscription when user == null =>
                BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.Unauthorized, "Sign in to a business account to subscribe to the SME plan."),
            PaymentPurposes.SmeSubscription => await CreateSubscriptionCheckoutAsync(user!, urls),
            _ => BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.Invalid, "Unknown purchase type.")
        };
    }

    private async Task<BillingResult<CheckoutResponseDto>> CreateReportCheckoutAsync(User? user, CheckoutRequestDto request, CheckoutUrls urls)
    {
        var email = user?.Email ?? NormaliseEmail(request.Email);
        if (email == null)
        {
            return BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.Invalid, "Enter a valid email address for your receipt.");
        }

        TaxReport? report;
        string? issuedToken = null;
        if (request.TaxReportId.HasValue)
        {
            report = await _reports.GetByIdAsync(request.TaxReportId.Value);
            if (report == null || !CanView(user?.Id, report, request.AccessToken))
            {
                return BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.NotFound, "Report not found.");
            }
        }
        else if (request.Calculation != null)
        {
            var created = await _reportService.CreateAsync(request.Calculation, user?.Id, user == null ? email : null);
            report = created.Entity;
            issuedToken = created.AccessToken;
            _logger.LogInformation("Created {Kind} report {ReportId} for checkout", user == null ? "guest" : "account", report.Id);
        }
        else
        {
            return BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.Invalid, "Choose the report you want to unlock.");
        }

        var token = issuedToken ?? request.AccessToken;
        if (await ResolveReportAccessAsync(user?.Id, report, token) != null)
        {
            return BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.AlreadyEntitled, "This report is already unlocked.",
                new CheckoutResponseDto { TaxReportId = report.Id, AccessToken = issuedToken });
        }

        var draft = new Payment
        {
            UserId = user?.Id,
            TaxReportId = report.Id,
            Purpose = PaymentPurposes.PremiumReport,
            Amount = Pricing.PriceFor(report.ReportType),
            Currency = Pricing.Currency,
            Email = email
        };

        return await StartTransactionAsync(draft, urls, issuedToken);
    }

    private async Task<BillingResult<CheckoutResponseDto>> CreateSubscriptionCheckoutAsync(User user, CheckoutUrls urls)
    {
        var current = await _subscriptions.GetLatestForUserAsync(user.Id);
        if (current != null && current.IsRecurring && current.IsActiveAt(UtcNow, Pricing.SubscriptionGraceDays) &&
            current.Status != SubscriptionStatuses.NonRenewing && current.Status != SubscriptionStatuses.Cancelled)
        {
            return BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.AlreadyEntitled, "Your SME plan is already active.");
        }

        var draft = new Payment
        {
            UserId = user.Id,
            Purpose = PaymentPurposes.SmeSubscription,
            Amount = Pricing.SmeSubscriptionPrice,
            Currency = Pricing.Currency,
            Email = user.Email,
            PlanCode = _subscriptionService.PlanCode
        };

        return await StartTransactionAsync(draft, urls, issuedToken: null);
    }

    /// <summary>
    /// Creates (or reuses) a pending payment and initialises the Paystack transaction.
    /// The payment row is written before calling Paystack so a reference always exists for the webhook.
    /// </summary>
    private async Task<BillingResult<CheckoutResponseDto>> StartTransactionAsync(Payment draft, CheckoutUrls urls, string? issuedToken)
    {
        CheckoutResponseDto Response(Payment p, bool reused) => new()
        {
            Reference = p.Reference,
            AuthorizationUrl = p.AuthorizationUrl ?? string.Empty,
            Amount = p.Amount,
            Currency = p.Currency,
            TaxReportId = p.TaxReportId,
            AccessToken = issuedToken,
            Reused = reused
        };

        if (!_gateway.IsConfigured)
        {
            return BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.PaymentsNotConfigured,
                "Online payments are not available yet. Please try again later.",
                new CheckoutResponseDto { TaxReportId = draft.TaxReportId, AccessToken = issuedToken });
        }

        // Duplicate-payment guard: reuse a recent unpaid checkout for the same item.
        var pending = await _payments.GetLatestPendingAsync(draft.Purpose, draft.TaxReportId, draft.TaxReportId.HasValue ? null : draft.UserId);
        if (pending != null)
        {
            var fresh = pending.CreatedAt > UtcNow.AddMinutes(-Pricing.PendingCheckoutMinutes);
            if (fresh && pending.AuthorizationUrl != null && pending.Amount == draft.Amount &&
                string.Equals(pending.Email, draft.Email, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Reusing pending checkout {Reference}", pending.Reference);
                return BillingResult<CheckoutResponseDto>.Ok(Response(pending, reused: true));
            }

            // Before superseding it, make sure the old checkout wasn't actually paid (external call, outside any transaction).
            var previous = await ConfirmAsync(pending);
            if (previous.Outcome == BillingOutcome.Ok)
            {
                return BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.AlreadyEntitled, "This purchase has already been paid.", Response(pending, reused: true));
            }
        }

        draft.Id = Guid.NewGuid();
        draft.Reference = NewReference();
        draft.Status = PaymentStatuses.Pending;
        draft.CreatedAt = UtcNow;

        try
        {
            // Expire the superseded checkout and insert the new one atomically. The filtered unique index
            // (one Pending payment per report) rejects a concurrent duplicate checkout.
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (pending != null && pending.Status == PaymentStatuses.Pending)
                {
                    pending.Status = PaymentStatuses.Expired;
                    pending.UpdatedAt = UtcNow;
                    await _payments.UpdateAsync(pending, ct);
                }
                await _payments.AddAsync(draft, ct);
            });
        }
        catch (Exception ex) when (ex is DuplicateRecordException or ConcurrencyConflictException)
        {
            // Another request opened a checkout for this item at the same moment: hand back that one.
            _logger.LogInformation("Concurrent checkout detected for {Purpose}; reusing the existing one", draft.Purpose);
            var winner = await _payments.GetLatestPendingAsync(draft.Purpose, draft.TaxReportId, draft.TaxReportId.HasValue ? null : draft.UserId);
            if (winner?.AuthorizationUrl != null)
            {
                return BillingResult<CheckoutResponseDto>.Ok(Response(winner, reused: true));
            }
            return BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.ProviderError, "A checkout is already being prepared. Please try again in a moment.");
        }

        var initiation = await _gateway.InitiatePaymentAsync(new PaymentRequest
        {
            Amount = draft.Amount,
            Currency = draft.Currency,
            Email = draft.Email,
            Reference = draft.Reference,
            PlanCode = draft.PlanCode,
            CallbackUrl = urls.CallbackUrl,
            CancelUrl = $"{urls.CancelUrl}{(urls.CancelUrl.Contains('?') ? '&' : '?')}reference={Uri.EscapeDataString(draft.Reference)}&cancelled=1",
            Metadata = new Dictionary<string, string>
            {
                ["purpose"] = draft.Purpose,
                ["payment_id"] = draft.Id.ToString(),
                ["tax_report_id"] = draft.TaxReportId?.ToString() ?? string.Empty
            }
        });

        if (!initiation.Success || string.IsNullOrEmpty(initiation.AuthorizationUrl))
        {
            draft.Status = PaymentStatuses.Failed;
            draft.FailureReason = "initialization_failed";
            draft.UpdatedAt = UtcNow;
            await _payments.UpdateAsync(draft);
            _logger.LogWarning("Paystack initialisation failed for {Reference}", draft.Reference);
            return BillingResult<CheckoutResponseDto>.Fail(BillingOutcome.ProviderError, "We couldn't start the payment. Please try again shortly.");
        }

        draft.AuthorizationUrl = initiation.AuthorizationUrl;
        draft.UpdatedAt = UtcNow;
        await _payments.UpdateAsync(draft);
        _logger.LogInformation("Checkout {Reference} started for {Purpose} ({AmountKobo} kobo)", draft.Reference, draft.Purpose, Money.ToKobo(draft.Amount));

        return BillingResult<CheckoutResponseDto>.Ok(Response(draft, reused: false));
    }

    // -----------------------------------------------------------------------
    // Verification & fulfilment
    // -----------------------------------------------------------------------

    public async Task<BillingResult<PaymentStatusDto>> VerifyAsync(Guid? userId, string reference)
    {
        var payment = await _payments.GetByReferenceAsync(reference);

        // Account payments are private to their owner; guest payments are addressed by their unguessable reference.
        if (payment == null || (payment.UserId.HasValue && payment.UserId != userId))
        {
            return BillingResult<PaymentStatusDto>.Fail(BillingOutcome.NotFound, "Payment not found.");
        }

        return await ConfirmAsync(payment);
    }

    /// <summary>
    /// Verifies a payment with Paystack (outside any transaction), then applies the outcome. A successful payment's
    /// status change and its fulfilment commit in one transaction. A write that loses a race to a concurrent
    /// verification is rejected by the row version, reloaded and re-evaluated — never blindly overwritten.
    /// </summary>
    private async Task<BillingResult<PaymentStatusDto>> ConfirmAsync(Payment payment, int conflictRetries = 2)
    {
        if (payment.Status == PaymentStatuses.Completed)
        {
            // Completed but not yet delivered (e.g. a crash between the two steps): finish delivery idempotently.
            if (payment.FulfilledAt == null)
            {
                await _unitOfWork.ExecuteInTransactionAsync(ct => FulfilOnceAsync(payment, ct));
            }
            return BillingResult<PaymentStatusDto>.Ok(ToDto(payment), "Payment confirmed.");
        }

        if (!_gateway.IsConfigured)
        {
            return BillingResult<PaymentStatusDto>.Fail(BillingOutcome.PaymentsNotConfigured, "Payments are not configured.", ToDto(payment));
        }

        var verification = await _gateway.VerifyPaymentAsync(payment.Reference);
        var now = UtcNow;

        try
        {
            if (verification.Success)
            {
                var problems = new List<string>();
                if (!string.Equals(verification.Reference, payment.Reference, StringComparison.Ordinal)) problems.Add("reference_mismatch");
                if (verification.AmountKobo != Money.ToKobo(payment.Amount)) problems.Add("amount_mismatch");
                if (!string.Equals(verification.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase)) problems.Add("currency_mismatch");

                if (problems.Count > 0)
                {
                    // Paid, but not what we charged for: never unlock automatically.
                    payment.Status = PaymentStatuses.Failed;
                    payment.FailureReason = string.Join(",", problems);
                    payment.ProviderTransactionId = verification.TransactionId;
                    payment.VerifiedAt = now;
                    payment.UpdatedAt = now;
                    await _payments.UpdateAsync(payment);
                    _logger.LogWarning("Payment {Reference} failed validation: {Problems}", payment.Reference, payment.FailureReason);
                    return BillingResult<PaymentStatusDto>.Fail(BillingOutcome.Failed, "The payment did not match this order. Please contact support with your reference.", ToDto(payment));
                }

                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    payment.Status = PaymentStatuses.Completed;
                    payment.FailureReason = null;
                    payment.PaidAt = verification.PaidAt ?? now;
                    payment.VerifiedAt = now;
                    payment.ProviderTransactionId = verification.TransactionId;
                    payment.Channel = verification.Channel;
                    payment.UpdatedAt = now;
                    await _payments.UpdateAsync(payment, ct);
                    await FulfilOnceAsync(payment, ct);
                });
                _logger.LogInformation("Payment {Reference} verified and confirmed", payment.Reference);
                return BillingResult<PaymentStatusDto>.Ok(ToDto(payment), "Payment confirmed.");
            }

            switch (verification.Status?.ToLowerInvariant())
            {
                case "failed":
                case "reversed":
                    payment.Status = PaymentStatuses.Failed;
                    payment.FailureReason = verification.Status!.ToLowerInvariant();
                    payment.UpdatedAt = now;
                    await _payments.UpdateAsync(payment);
                    return BillingResult<PaymentStatusDto>.Fail(BillingOutcome.Failed, "The payment was declined. No money was taken — you can try again.", ToDto(payment));

                case "abandoned":
                    // Not final at Paystack: a later successful charge on this reference still completes it.
                    if (payment.Status == PaymentStatuses.Pending)
                    {
                        payment.Status = PaymentStatuses.Cancelled;
                        payment.UpdatedAt = now;
                        await _payments.UpdateAsync(payment);
                    }
                    return BillingResult<PaymentStatusDto>.Fail(BillingOutcome.Cancelled, "The payment was cancelled. You have not been charged.", ToDto(payment));

                default:
                    return BillingResult<PaymentStatusDto>.Fail(BillingOutcome.Pending, "Your payment is still being processed.", ToDto(payment));
            }
        }
        catch (ConcurrencyConflictException) when (conflictRetries > 0)
        {
            // Another verification changed this payment first (e.g. the webhook completed it). Re-read and
            // re-evaluate from the current state instead of overwriting it.
            _logger.LogInformation("Payment {Reference} changed concurrently; re-evaluating", payment.Reference);
            await _payments.ReloadAsync(payment);
            return await ConfirmAsync(payment, conflictRetries - 1);
        }
    }

    /// <summary>Delivers a completed payment exactly once. Must run inside the caller's transaction.</summary>
    private async Task FulfilOnceAsync(Payment payment, CancellationToken cancellationToken)
    {
        var now = UtcNow;

        if (payment.Purpose == PaymentPurposes.PremiumReport && payment.TaxReportId.HasValue)
        {
            var report = await _reports.GetByIdAsync(payment.TaxReportId.Value, cancellationToken);
            if (report != null && !report.IsUnlocked)
            {
                report.IsUnlocked = true;
                report.UnlockedAt = now;
                await _reports.UpdateAsync(report, cancellationToken);
            }

            if (await _payments.TryMarkFulfilledAsync(payment.Id, now, cancellationToken))
            {
                _logger.LogInformation("Report {ReportId} unlocked by payment {Reference}", payment.TaxReportId, payment.Reference);
            }
            return;
        }

        if (payment.Purpose == PaymentPurposes.SmeSubscription)
        {
            // Extending access is not idempotent, so claim first: only one caller may activate.
            if (!await _payments.TryMarkFulfilledAsync(payment.Id, now, cancellationToken))
            {
                return;
            }

            var subscription = await _subscriptionService.ActivateFromPaymentAsync(payment, cancellationToken);
            if (subscription != null && payment.SubscriptionId != subscription.Id)
            {
                payment.SubscriptionId = subscription.Id;
                payment.UpdatedAt = now;
                await _payments.UpdateAsync(payment, cancellationToken);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Webhooks: persisted on receipt, processed separately, retried on failure
    // -----------------------------------------------------------------------

    public async Task<WebhookResult> HandleWebhookAsync(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var eventType = Str(root, "event") ?? "unknown";
        var data = root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : default;
        var reference = Str(data, "reference") ?? Str(data, "subscription_code") ?? Str(Obj(data, "subscription"), "subscription_code");
        var eventKey = BuildEventKey(eventType, data, payload);

        var existing = await _webhookEvents.GetByKeyAsync(eventKey);
        if (existing?.Status == WebhookEventStatuses.Processed)
        {
            _logger.LogInformation("Duplicate webhook {EventType} ignored", eventType);
            return new WebhookResult(eventType, "duplicate");
        }
        if (existing?.Status == WebhookEventStatuses.Processing && existing.LastAttemptAt > UtcNow.AddMinutes(-2))
        {
            // Another delivery is being processed right now; it (or the retry worker) will finish it.
            return new WebhookResult(eventType, "in_progress");
        }

        var record = existing;
        if (record == null)
        {
            // 1. Persist the event before doing any business work.
            record = new PaymentWebhookEvent
            {
                Id = Guid.NewGuid(),
                EventKey = eventKey,
                EventType = eventType,
                Reference = reference,
                Payload = payload,
                Status = WebhookEventStatuses.Received,
                ReceivedAt = UtcNow
            };
            if (!await _webhookEvents.TryAddAsync(record))
            {
                return new WebhookResult(eventType, "duplicate");
            }
        }

        // 2. Process. Failures are recorded on the event and re-thrown so Paystack retries too.
        return await ProcessEventAsync(record, rethrowOnFailure: true);
    }

    public async Task<int> RetryPendingWebhooksAsync(int maxAttempts, TimeSpan retryAfter, int batchSize, CancellationToken cancellationToken = default)
    {
        var events = await _webhookEvents.GetRetryableAsync(maxAttempts, UtcNow - retryAfter, batchSize, cancellationToken);
        var processed = 0;
        foreach (var webhookEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ProcessEventAsync(webhookEvent, rethrowOnFailure: false);
            if (result.Outcome != "failed")
            {
                processed++;
            }
        }
        return processed;
    }

    private async Task<WebhookResult> ProcessEventAsync(PaymentWebhookEvent record, bool rethrowOnFailure)
    {
        record.Attempts++;
        record.LastAttemptAt = UtcNow;
        record.Status = WebhookEventStatuses.Processing;
        await _webhookEvents.UpdateAsync(record);

        try
        {
            var outcome = await DispatchEventAsync(record.EventType, record.Payload);

            // 3. Only now — after every business change has committed — mark the event processed.
            record.Status = WebhookEventStatuses.Processed;
            record.ProcessedAt = UtcNow;
            record.Outcome = outcome;
            record.LastError = null;
            await _webhookEvents.UpdateAsync(record);
            _logger.LogInformation("Webhook {EventType} processed: {Outcome} (attempt {Attempt})", record.EventType, outcome, record.Attempts);
            return new WebhookResult(record.EventType, outcome);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            record.Status = WebhookEventStatuses.Failed;
            record.Outcome = "failed";
            record.LastError = Truncate($"{ex.GetType().Name}: {ex.Message}", 1000);
            await _webhookEvents.UpdateAsync(record);
            _logger.LogError(ex, "Webhook {EventType} processing failed (attempt {Attempt})", record.EventType, record.Attempts);

            if (rethrowOnFailure)
            {
                throw;
            }
            return new WebhookResult(record.EventType, "failed");
        }
    }

    private async Task<string> DispatchEventAsync(string eventType, string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var data = document.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : default;
        var reference = Str(data, "reference");
        var subscriptionCode = Str(data, "subscription_code") ?? Str(Obj(data, "subscription"), "subscription_code");
        var customerEmail = Str(Obj(data, "customer"), "email");

        switch (eventType)
        {
            case "charge.success" when reference != null:
                var payment = await _payments.GetByReferenceAsync(reference);
                if (payment != null)
                {
                    // Never trust the body: ConfirmAsync re-verifies with Paystack before changing anything.
                    return (await ConfirmAsync(payment)).Outcome.ToString();
                }
                if (data.TryGetProperty("plan", out var plan) && plan.ValueKind == JsonValueKind.Object && plan.EnumerateObject().Any())
                {
                    // Recurring renewal: Paystack generated this transaction, not us.
                    await _subscriptionService.RecordRenewalChargeAsync(reference);
                    return "renewal_recorded";
                }
                return "unknown_reference";

            case "subscription.create":
            case "subscription.enable":
            case "subscription.not_renew":
            case "subscription.disable":
            case "subscription.expiring_cards":
            case "invoice.create":
            case "invoice.update":
                if (subscriptionCode == null) return "missing_subscription_code";
                await _subscriptionService.SyncAsync(subscriptionCode, customerEmail);
                return "subscription_synced";

            case "invoice.payment_failed":
                if (subscriptionCode == null) return "missing_subscription_code";
                await _subscriptionService.MarkRenewalFailedAsync(subscriptionCode);
                await _subscriptionService.SyncAsync(subscriptionCode, customerEmail);
                return "renewal_failed_recorded";

            default:
                return "ignored";
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static string BuildEventKey(string eventType, JsonElement data, string payload)
    {
        var id = Str(data, "id") ?? Str(data, "invoice_code") ?? Str(data, "reference") ?? Str(data, "subscription_code");
        if (id == null)
        {
            id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..32];
        }
        var status = Str(data, "status") ?? string.Empty;
        var key = $"{eventType}:{id}:{status}";
        return key.Length > 200 ? key[..200] : key;
    }

    private static JsonElement Obj(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : default;

    private static string? Str(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    // -----------------------------------------------------------------------
    // History & claiming guest reports
    // -----------------------------------------------------------------------

    public async Task<IReadOnlyList<PaymentStatusDto>> GetPaymentHistoryAsync(Guid userId)
    {
        var payments = await _payments.GetByUserIdAsync(userId);
        return payments.Where(p => p.Status != PaymentStatuses.Expired).Select(ToDto).ToList();
    }

    public async Task<BillingResult<Guid>> ClaimGuestReportAsync(Guid userId, Guid reportId, string? accessToken)
    {
        var report = await _reports.GetByIdAsync(reportId);
        if (report == null)
        {
            return BillingResult<Guid>.Fail(BillingOutcome.NotFound, "Report not found.");
        }
        if (report.UserId == userId)
        {
            return BillingResult<Guid>.Ok(report.Id, "Already in your account.");
        }
        if (report.UserId != null || !ReportAccessTokens.Matches(accessToken, report.AccessTokenHash))
        {
            return BillingResult<Guid>.Fail(BillingOutcome.NotFound, "Report not found.");
        }

        // The report and its payments move to the account together, or not at all.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            report.UserId = userId;
            report.AccessTokenHash = null;
            await _reports.UpdateAsync(report, ct);

            foreach (var payment in await _payments.GetByTaxReportIdAsync(report.Id, ct))
            {
                if (payment.UserId == null)
                {
                    payment.UserId = userId;
                    payment.UpdatedAt = UtcNow;
                    await _payments.UpdateAsync(payment, ct);
                }
            }
        });

        _logger.LogInformation("Guest report {ReportId} claimed into an account", report.Id);
        return BillingResult<Guid>.Ok(report.Id, "Report saved to your account.");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string NewReference() => $"TNV-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid():N}";

    private static string? NormaliseEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
        {
            return null;
        }
        try
        {
            var address = new MailAddress(email.Trim());
            return address.Address == email.Trim() && address.Host.Contains('.') ? address.Address.ToLowerInvariant() : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static PaymentStatusDto ToDto(Payment payment) => new()
    {
        Reference = payment.Reference,
        Purpose = payment.Purpose,
        Status = payment.Status,
        Amount = payment.Amount,
        Currency = payment.Currency,
        TaxReportId = payment.TaxReportId,
        IsGuest = payment.UserId == null,
        CreatedAt = payment.CreatedAt,
        PaidAt = payment.PaidAt
    };
}
