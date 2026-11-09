namespace TaxNavi.Infrastructure.Repositories;

using Npgsql;
using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using Data;

public class PaymentWebhookEventRepository : IPaymentWebhookEventRepository
{
    private readonly TaxNaviDbContext _context;

    public PaymentWebhookEventRepository(TaxNaviDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentWebhookEvent?> GetByKeyAsync(string eventKey, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentWebhookEvents.FirstOrDefaultAsync(e => e.EventKey == eventKey, cancellationToken);
    }

    public async Task<bool> TryAddAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        _context.PaymentWebhookEvents.Add(webhookEvent);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Unique index on EventKey: a concurrent delivery of the same event won the insert.
            // Any other database error is a real failure and propagates.
            _context.Entry(webhookEvent).State = EntityState.Detached;
            return false;
        }
    }

    public async Task UpdateAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        _context.PaymentWebhookEvents.Update(webhookEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentWebhookEvent>> GetRetryableAsync(int maxAttempts, DateTime staleBefore, int take, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentWebhookEvents
            .Where(e => e.Attempts < maxAttempts)
            .Where(e => e.Status == WebhookEventStatuses.Received
                        || e.Status == WebhookEventStatuses.Failed
                        || e.Status == WebhookEventStatuses.Processing)
            .Where(e => e.LastAttemptAt == null || e.LastAttemptAt < staleBefore)
            .OrderBy(e => e.ReceivedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
