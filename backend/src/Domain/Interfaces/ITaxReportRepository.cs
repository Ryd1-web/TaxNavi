namespace TaxNavi.Domain.Interfaces;

using Entities;

public interface ITaxReportRepository : IRepository<TaxReport>
{
    Task<IEnumerable<TaxReport>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TaxReport?> GetForUserAsync(Guid reportId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>True if any report still uses the given calculation profile.</summary>
    Task<bool> AnyForProfileAsync(Guid taxProfileId, CancellationToken cancellationToken = default);
}
