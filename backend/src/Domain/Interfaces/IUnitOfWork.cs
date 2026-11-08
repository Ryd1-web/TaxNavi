namespace TaxNavi.Domain.Interfaces;

/// <summary>
/// Runs work in a single database transaction so related writes commit or roll back together.
/// Never call external services (e.g. Paystack) inside the delegate: keep transactions short.
/// Nested calls join the outer transaction.
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
}
