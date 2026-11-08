using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;

namespace TaxNavi.Api.Tests.Relational;

/// <summary>Controllable Paystack stand-in for relational tests.</summary>
public sealed class SqlTestGateway : IPaymentService
{
    public Dictionary<string, PaymentVerificationResponse> Verifications { get; } = new();
    public Dictionary<string, ProviderSubscription> RemoteSubscriptions { get; } = new();

    /// <summary>Number of upcoming VerifyPaymentAsync calls that throw (simulates a Paystack/network outage).</summary>
    public int FailNextVerifications { get; set; }

    public bool IsConfigured => true;
    public bool IsTestMode => true;

    public Task<PaymentInitiationResponse> InitiatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentInitiationResponse { Success = true, Reference = request.Reference, AuthorizationUrl = $"https://checkout.paystack.test/{request.Reference}" });

    public Task<PaymentVerificationResponse> VerifyPaymentAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (FailNextVerifications > 0)
        {
            FailNextVerifications--;
            throw new HttpRequestException("Paystack unavailable (simulated)");
        }
        return Task.FromResult(Verifications.TryGetValue(reference, out var v) ? v : new PaymentVerificationResponse { Status = "ongoing", Reference = reference });
    }

    public void Paid(string reference, decimal naira, string email, string? plan = null, string status = "success") =>
        Verifications[reference] = new PaymentVerificationResponse
        {
            Success = status == "success",
            Status = status,
            Reference = reference,
            AmountKobo = Money.ToKobo(naira),
            Currency = "NGN",
            TransactionId = Math.Abs(reference.GetHashCode()),
            Channel = "card",
            CustomerEmail = email,
            PlanCode = plan,
            PaidAt = DateTime.UtcNow
        };

    public bool IsValidWebhookSignature(byte[] rawBody, string? signature) => true;

    public Task<ProviderSubscription?> FetchSubscriptionAsync(string subscriptionCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(RemoteSubscriptions.TryGetValue(subscriptionCode, out var s) ? s : null);

    public Task<bool> DisableSubscriptionAsync(string subscriptionCode, string emailToken, CancellationToken cancellationToken = default)
    {
        if (RemoteSubscriptions.TryGetValue(subscriptionCode, out var s) && s.EmailToken == emailToken)
        {
            s.Status = "non-renewing";
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<string?> GetSubscriptionManageLinkAsync(string subscriptionCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>("https://paystack.test/manage");
}
