using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;
using TaxNavi.Infrastructure.Data;

namespace TaxNavi.Infrastructure.Repositories;

/// <summary>
/// pgvector-backed chunk store. Similarity search runs inside PostgreSQL using the HNSW
/// index (cosine distance); scores are returned as similarity = 1 − distance.
/// </summary>
public class DocumentChunkRepository : IVectorSearchService
{
    private readonly TaxNaviDbContext _context;

    public DocumentChunkRepository(TaxNaviDbContext context) => _context = context;

    public async Task ReplaceDocumentChunksAsync(Guid documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _context.DocumentChunks.Where(c => c.DocumentId == documentId).ExecuteDeleteAsync(cancellationToken);
        _context.DocumentChunks.AddRange(chunks);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        double minSimilarity,
        CancellationToken cancellationToken = default)
    {
        var query = new Vector(queryEmbedding);
        var maxDistance = 1.0 - minSimilarity;

        var hits = await _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.Document.Status == KnowledgeDocumentStatuses.Indexed)
            .Select(c => new
            {
                Chunk = c,
                Distance = c.Embedding.CosineDistance(query)
            })
            .OrderBy(x => x.Distance)
            .Take(topK)
            .ToListAsync(cancellationToken);

        var documentIds = hits.Select(h => h.Chunk.DocumentId).Distinct().ToList();

        // Metadata only — never pull FileContent (the whole PDF) into a search.
        var documents = await _context.KnowledgeDocuments
            .AsNoTracking()
            .Where(d => documentIds.Contains(d.Id))
            .Select(d => new KnowledgeDocument
            {
                Id = d.Id,
                Title = d.Title,
                DocumentType = d.DocumentType,
                SourceName = d.SourceName,
                SourceUrl = d.SourceUrl,
                TaxYear = d.TaxYear,
                Version = d.Version,
                EffectiveDate = d.EffectiveDate,
                Status = d.Status
            })
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        return hits
            .Where(h => h.Distance <= maxDistance)
            .Select(h => new RetrievedChunk(h.Chunk, documents[h.Chunk.DocumentId], 1.0 - h.Distance))
            .ToList();
    }
}
