namespace TaxNavi.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using Data;

public class TaxReportRepository : BaseRepository<TaxReport>, ITaxReportRepository
{
    public TaxReportRepository(TaxNaviDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TaxReport>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.TaxReports
            .Where(tr => tr.UserId == userId)
            .OrderByDescending(tr => tr.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaxReport?> GetForUserAsync(Guid reportId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.TaxReports.FirstOrDefaultAsync(tr => tr.Id == reportId && tr.UserId == userId, cancellationToken);
    }

    public async Task<bool> AnyForProfileAsync(Guid taxProfileId, CancellationToken cancellationToken = default)
    {
        return await _context.TaxReports.AnyAsync(tr => tr.TaxProfileId == taxProfileId, cancellationToken);
    }
}
