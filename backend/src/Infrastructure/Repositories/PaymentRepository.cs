namespace TaxNavi.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using Data;

public class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(TaxNaviDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Payment?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        return await _context.Payments.FirstOrDefaultAsync(p => p.Reference == reference, cancellationToken);
    }

    public async Task<IEnumerable<Payment>> GetByTaxReportIdAsync(Guid taxReportId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments.Where(p => p.TaxReportId == taxReportId).ToListAsync(cancellationToken);
    }

    public async Task<Payment?> GetLatestPendingAsync(string purpose, Guid? taxReportId, Guid? userId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.Status == PaymentStatuses.Pending && p.Purpose == purpose)
            .Where(p => taxReportId.HasValue ? p.TaxReportId == taxReportId : p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryMarkFulfilledAsync(Guid paymentId, DateTime fulfilledAt, CancellationToken cancellationToken = default)
    {
        if (_context.Database.IsRelational())
        {
            // Single conditional UPDATE: the database guarantees only one caller sees a row change.
            // It runs inside the caller's transaction when there is one.
            var affected = await _context.Payments
                .Where(p => p.Id == paymentId && p.FulfilledAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.FulfilledAt, fulfilledAt), cancellationToken);

            if (affected == 1)
            {
                // Keep a tracked instance in step without marking it dirty, so a later SaveChanges can't overwrite it.
                var tracked = _context.Payments.Local.FirstOrDefault(p => p.Id == paymentId);
                if (tracked != null)
                {
                    // The UPDATE also bumped the row version: refresh it so later saves of this instance don't
                    // report a false concurrency conflict.
                    var entry = _context.Entry(tracked);
                    var version = await _context.Payments.Where(p => p.Id == paymentId).Select(p => p.Version).SingleAsync(cancellationToken);
                    tracked.FulfilledAt = fulfilledAt;
                    entry.Property(p => p.FulfilledAt).OriginalValue = fulfilledAt;
                    entry.Property(p => p.Version).CurrentValue = version;
                    entry.Property(p => p.Version).OriginalValue = version;
                    entry.Property(p => p.FulfilledAt).IsModified = false;
                }
            }
            return affected == 1;
        }

        // In-memory provider (local development only): no bulk updates, so check-and-set within this context.
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment == null || payment.FulfilledAt != null)
        {
            return false;
        }
        payment.FulfilledAt = fulfilledAt;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReloadAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        var entry = _context.Entry(payment);
        if (entry.State == EntityState.Detached)
        {
            _context.Payments.Attach(payment);
        }
        await entry.ReloadAsync(cancellationToken);
    }
}
