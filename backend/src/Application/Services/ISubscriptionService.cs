using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Entities;

namespace TaxNavi.Application.Services;

public interface ISubscriptionService
{
    bool IsRecurring { get; }
    string? PlanCode { get; }

    /// <summary>
    /// Grants the SME plan after a verified first payment and returns the subscription it created or extended.
    /// Called exactly once per payment, inside the caller's fulfilment transaction.
    /// </summary>
    Task<Subscription?> ActivateFromPaymentAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>Re-reads the subscription from Paystack (the source of truth) and updates local state.</summary>
    Task SyncAsync(string subscriptionCode, string? customerEmailHint);
    Task MarkRenewalFailedAsync(string subscriptionCode);

    /// <summary>Records a Paystack-generated renewal charge after verifying it with Paystack.</summary>
    Task RecordRenewalChargeAsync(string reference);
    Task<BillingResult<bool>> CancelAsync(Guid userId);
    Task<BillingResult<string>> GetManageLinkAsync(Guid userId);
}
