namespace TaxNavi.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using Data;

public class TaxProfileRepository : BaseRepository<TaxProfile>, ITaxProfileRepository
{
    public TaxProfileRepository(TaxNaviDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TaxProfile>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.TaxProfiles.Where(tp => tp.UserId == userId).ToListAsync(cancellationToken);
    }
}
