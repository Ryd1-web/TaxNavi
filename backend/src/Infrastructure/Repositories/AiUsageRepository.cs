using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Infrastructure.Data;

namespace TaxNavi.Infrastructure.Repositories;

public class AiUsageRepository : IAiUsageRepository
{
    private readonly TaxNaviDbContext _context;

    public AiUsageRepository(TaxNaviDbContext context) => _context = context;

    public async Task AddAsync(AiUsageRecord record, CancellationToken cancellationToken = default)
    {
        _context.AiUsageRecords.Add(record);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountSinceAsync(Guid? userId, string? clientKey, DateTime since, CancellationToken cancellationToken = default) =>
        _context.AiUsageRecords.CountAsync(r =>
            r.CreatedAt >= since &&
            r.Success &&
            (r.Operation == AiOperations.Chat || r.Operation == AiOperations.ExplainReport) &&
            (userId != null ? r.UserId == userId : r.ClientKey == clientKey),
            cancellationToken);
}
