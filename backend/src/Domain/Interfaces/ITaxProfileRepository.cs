namespace TaxNavi.Domain.Interfaces;

using Entities;

public interface ITaxProfileRepository : IRepository<TaxProfile>
{
    Task<IEnumerable<TaxProfile>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
