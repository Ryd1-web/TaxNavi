namespace TaxNavi.Domain.Interfaces;

using Entities;

public interface ISubscriptionRepository : IRepository<Subscription>
{
    Task<Subscription?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Subscription?> GetByProviderCodeAsync(string subscriptionCode, CancellationToken cancellationToken = default);
}
