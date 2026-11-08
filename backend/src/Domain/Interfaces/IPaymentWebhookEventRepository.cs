namespace TaxNavi.Domain.Interfaces;

using Entities;

public interface IPaymentWebhookEventRepository
{
    Task<PaymentWebhookEvent?> GetByKeyAsync(string eventKey, CancellationToken cancellationToken = default);

    /// <summary>Inserts the event; returns false only if another delivery already recorded the same key.</summary>
    Task<bool> TryAddAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken = default);
    Task UpdateAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Events that still need processing — received, failed, or stuck "Processing" since before
    /// <paramref name="staleBefore"/> — with fewer than <paramref name="maxAttempts"/> attempts. Oldest first.
    /// </summary>
    Task<IReadOnlyList<PaymentWebhookEvent>> GetRetryableAsync(int maxAttempts, DateTime staleBefore, int take, CancellationToken cancellationToken = default);
}
