namespace TaxNavi.Domain.Interfaces;

using Entities;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);
    Task<IEnumerable<Payment>> GetByTaxReportIdAsync(Guid taxReportId, CancellationToken cancellationToken = default);

    /// <summary>Most recent unpaid checkout for an item, used to avoid creating duplicate transactions.</summary>
    Task<Payment?> GetLatestPendingAsync(string purpose, Guid? taxReportId, Guid? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically sets FulfilledAt if it is still null. Returns true only for the single caller that wins,
    /// so concurrent webhook and redirect verifications can never fulfil the same payment twice.
    /// </summary>
    Task<bool> TryMarkFulfilledAsync(Guid paymentId, DateTime fulfilledAt, CancellationToken cancellationToken = default);

    /// <summary>Discards local changes and reloads the payment's current database state (after a concurrency conflict).</summary>
    Task ReloadAsync(Payment payment, CancellationToken cancellationToken = default);
}
