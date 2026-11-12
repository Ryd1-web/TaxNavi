using TaxNavi.Domain.Entities;

namespace TaxNavi.Domain.Interfaces;

public interface IAiUsageRepository
{
    Task AddAsync(AiUsageRecord record, CancellationToken cancellationToken = default);

    /// <summary>Successful LLM calls since <paramref name="since"/> for a user or anonymous client key (daily quota check).</summary>
    Task<int> CountSinceAsync(Guid? userId, string? clientKey, DateTime since, CancellationToken cancellationToken = default);
}
