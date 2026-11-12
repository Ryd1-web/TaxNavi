using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;

namespace TaxNavi.Infrastructure.Services;

/// <summary>
/// Knowledge-base ingestion: register a PDF (deduplicated by content hash, versioned by title),
/// then extract → chunk → embed → store. Embedding happens before the database transaction;
/// the swap of chunks + status flip + superseding of older versions is atomic.
/// </summary>
public class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IKnowledgeDocumentRepository _documents;
    private readonly IVectorSearchService _vectors;
    private readonly IEmbeddingService _embeddings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IKnowledgeDocumentRepository documents,
        IVectorSearchService vectors,
        IEmbeddingService embeddings,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        ILogger<DocumentIngestionService> logger)
    {
        _documents = documents;
        _vectors = vectors;
        _embeddings = embeddings;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<KnowledgeDocument> RegisterAsync(KnowledgeDocument metadata, byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        if (pdfBytes.Length == 0)
        {
            throw new ArgumentException("The uploaded file is empty.", nameof(pdfBytes));
        }

        var hash = Convert.ToHexString(SHA256.HashData(pdfBytes));
        var existing = await _documents.FindByContentHashAsync(hash, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("Knowledge document {Id} already registered (same content hash)", existing.Id);
            return existing;
        }

        metadata.Id = metadata.Id == Guid.Empty ? Guid.NewGuid() : metadata.Id;
        metadata.ContentHash = hash;
        metadata.FileContent = pdfBytes;
        metadata.Status = KnowledgeDocumentStatuses.Pending;
        metadata.CreatedAt = _clock.GetUtcNow().UtcDateTime;
        metadata.Version = await _documents.NextVersionAsync(metadata.Title, cancellationToken);
        await _documents.AddAsync(metadata, cancellationToken);
        _logger.LogInformation("Registered knowledge document {Id} \"{Title}\" v{Version}", metadata.Id, metadata.Title, metadata.Version);
        return metadata;
    }

    public async Task<KnowledgeDocument> IndexAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _documents.GetByIdAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Knowledge document {documentId} not found.");

        document.Status = KnowledgeDocumentStatuses.Processing;
        document.LastError = null;
        await _documents.UpdateAsync(document, cancellationToken);

        try
        {
            var pages = PdfTextExtractor.Extract(document.FileContent);
            var drafts = LegislationChunker.Chunk(pages, document.DocumentType == KnowledgeDocumentTypes.Legislation);
            if (drafts.Count == 0)
            {
                throw new InvalidOperationException("No text could be extracted from the PDF (is it a scanned image?).");
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            var chunks = drafts.Select((draft, index) => new DocumentChunk
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                ChunkIndex = index,
                Content = draft.Content,
                Chapter = draft.Chapter,
                Part = draft.Part,
                SectionNumber = draft.SectionNumber,
                SectionTitle = draft.SectionTitle,
                PageFrom = draft.PageFrom,
                PageTo = draft.PageTo,
                TokenCount = draft.Content.Length / 4,
                CreatedAt = now
            }).ToList();

            // External call stays outside the transaction (project rule).
            var embeddingTexts = chunks.Select(c => ChunkFormat.EmbeddingText(document, c)).ToList();
            var vectors = await _embeddings.EmbedDocumentsAsync(embeddingTexts, cancellationToken);
            for (var i = 0; i < chunks.Count; i++)
            {
                chunks[i].Embedding = new Pgvector.Vector(vectors[i]);
            }

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _vectors.ReplaceDocumentChunksAsync(document.Id, chunks, ct);
                document.PageCount = pages.Count;
                document.ChunkCount = chunks.Count;
                document.Status = KnowledgeDocumentStatuses.Indexed;
                document.IndexedAt = _clock.GetUtcNow().UtcDateTime;
                await _documents.UpdateAsync(document, ct);

                // Older versions stop being searched but are never deleted (versioning rule).
                await _documents.SupersedeOtherIndexedVersionsAsync(document.Title, document.Id, ct);
            }, cancellationToken);

            _logger.LogInformation("Indexed knowledge document {Id}: {Pages} pages → {Chunks} chunks", document.Id, pages.Count, chunks.Count);
            return document;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Indexing failed for knowledge document {Id}", document.Id);
            try
            {
                document.Status = KnowledgeDocumentStatuses.Failed;
                document.LastError = ex.GetType().Name + ": " + ex.Message;
                await _documents.UpdateAsync(document, CancellationToken.None);
            }
            catch (Exception statusEx)
            {
                // Never let bookkeeping hide the root cause.
                _logger.LogError(statusEx, "Could not record the failed status for knowledge document {Id}", document.Id);
            }

            throw;
        }
    }
}
