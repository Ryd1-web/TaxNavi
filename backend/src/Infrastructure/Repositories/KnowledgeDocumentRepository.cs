using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Infrastructure.Data;

namespace TaxNavi.Infrastructure.Repositories;

public class KnowledgeDocumentRepository : BaseRepository<KnowledgeDocument>, IKnowledgeDocumentRepository
{
    public KnowledgeDocumentRepository(TaxNaviDbContext context) : base(context) { }

    public Task<KnowledgeDocument?> FindByContentHashAsync(string contentHash, CancellationToken cancellationToken = default) =>
        _context.KnowledgeDocuments.FirstOrDefaultAsync(d => d.ContentHash == contentHash, cancellationToken);

    /// <summary>Listing never loads PDF bytes; FileContent stays in the database until indexing needs it.</summary>
    public Task<List<KnowledgeDocument>> ListAsync(CancellationToken cancellationToken = default) =>
        _context.KnowledgeDocuments
            .AsNoTracking() // read-only projections must never enter the identity map
            .Select(d => new KnowledgeDocument
            {
                Id = d.Id,
                Title = d.Title,
                DocumentType = d.DocumentType,
                SourceName = d.SourceName,
                SourceUrl = d.SourceUrl,
                PublicationDate = d.PublicationDate,
                EffectiveDate = d.EffectiveDate,
                TaxYear = d.TaxYear,
                Version = d.Version,
                SupersedesDocumentId = d.SupersedesDocumentId,
                ContentHash = d.ContentHash,
                Status = d.Status,
                LastError = d.LastError,
                FileName = d.FileName,
                PageCount = d.PageCount,
                ChunkCount = d.ChunkCount,
                CreatedAt = d.CreatedAt,
                IndexedAt = d.IndexedAt
            })
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<int> SupersedeOtherIndexedVersionsAsync(string title, Guid currentId, CancellationToken cancellationToken = default) =>
        _context.KnowledgeDocuments
            .Where(d => d.Title == title && d.Id != currentId && d.Status == KnowledgeDocumentStatuses.Indexed)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, KnowledgeDocumentStatuses.Superseded), cancellationToken);

    public async Task<int> NextVersionAsync(string title, CancellationToken cancellationToken = default)
    {
        var current = await _context.KnowledgeDocuments
            .Where(d => d.Title == title)
            .MaxAsync(d => (int?)d.Version, cancellationToken);
        return (current ?? 0) + 1;
    }
}
