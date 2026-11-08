using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Entities;

namespace TaxNavi.Application.Services;

public interface IBillingService
{
    bool PaymentsEnabled { get; }
    bool TestMode { get; }
    decimal ReportPriceFor(string reportType);

    /// <summary>How the caller is entitled to the full report, or null. Guests prove access with the report access token.</summary>
    Task<string?> ResolveReportAccessAsync(Guid? userId, TaxReport report, string? accessToken = null);
    bool CanView(Guid? userId, TaxReport report, string? accessToken);

    Task<EntitlementsDto> GetEntitlementsAsync(Guid userId);
    Task<BillingResult<CheckoutResponseDto>> CreateCheckoutAsync(Guid? userId, CheckoutRequestDto request, CheckoutUrls urls);
    Task<BillingResult<PaymentStatusDto>> VerifyAsync(Guid? userId, string reference);

    /// <summary>
    /// Persists a webhook whose signature the caller has already validated against the raw body, then processes it.
    /// Processing failures are recorded on the stored event and re-thrown.
    /// </summary>
    Task<WebhookResult> HandleWebhookAsync(string payload);

    /// <summary>Re-processes stored webhook events that failed or were never completed. Returns how many succeeded.</summary>
    Task<int> RetryPendingWebhooksAsync(int maxAttempts, TimeSpan retryAfter, int batchSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentStatusDto>> GetPaymentHistoryAsync(Guid userId);
    Task<BillingResult<Guid>> ClaimGuestReportAsync(Guid userId, Guid reportId, string? accessToken);
}
