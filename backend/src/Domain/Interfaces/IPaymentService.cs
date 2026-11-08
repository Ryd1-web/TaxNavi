namespace TaxNavi.Domain.Interfaces;

using Entities;
using TaxNavi.Domain.Models;

/// <summary>Payment gateway abstraction. Implementations must never report success without provider confirmation.</summary>
public interface IPaymentService
{
    bool IsConfigured { get; }
    bool IsTestMode { get; }
    Task<PaymentInitiationResponse> InitiatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentVerificationResponse> VerifyPaymentAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Validates the provider signature over the exact raw request bytes.</summary>
    bool IsValidWebhookSignature(byte[] rawBody, string? signature);

    Task<ProviderSubscription?> FetchSubscriptionAsync(string subscriptionCode, CancellationToken cancellationToken = default);
    Task<bool> DisableSubscriptionAsync(string subscriptionCode, string emailToken, CancellationToken cancellationToken = default);
    Task<string?> GetSubscriptionManageLinkAsync(string subscriptionCode, CancellationToken cancellationToken = default);
}
