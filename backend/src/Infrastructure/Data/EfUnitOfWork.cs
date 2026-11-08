namespace TaxNavi.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Interfaces;

/// <summary>
/// EF Core transaction boundary. On failure the transaction rolls back and the change tracker is cleared,
/// so no half-applied entity state can be saved later in the same request.
/// </summary>
public class EfUnitOfWork : IUnitOfWork
{
    private readonly TaxNaviDbContext _context;

    public EfUnitOfWork(TaxNaviDbContext context)
    {
        _context = context;
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async ct =>
        {
            await work(ct);
            return true;
        }, cancellationToken);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        // The in-memory provider (local development only) has no transactions; nested calls join the outer one.
        if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction != null)
        {
            return await work(cancellationToken);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await work(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
    }
}
