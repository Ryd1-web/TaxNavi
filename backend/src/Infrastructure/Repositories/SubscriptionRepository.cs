namespace TaxNavi.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using Data;

public class SubscriptionRepository : BaseRepository<Subscription>, ISubscriptionRepository
{
    public SubscriptionRepository(TaxNaviDbContext context) : base(context)
    {
    }

    public async Task<Subscription?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CurrentPeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Subscription?> GetByProviderCodeAsync(string subscriptionCode, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions.FirstOrDefaultAsync(s => s.ProviderSubscriptionCode == subscriptionCode, cancellationToken);
    }
}
