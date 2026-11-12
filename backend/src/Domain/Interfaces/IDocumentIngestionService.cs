using TaxNavi.Domain.Entities;

namespace TaxNavi.Domain.Interfaces;

/// <summary>Registers a PDF in the knowledge base and runs extract → chunk → embed → store.</summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// Create (or version-bump) a knowledge document from PDF bytes. Returns the existing
    /// document unchanged when the same content hash is already registered.
    /// </summary>
    Task<KnowledgeDocument> RegisterAsync(KnowledgeDocument metadata, byte[] pdfBytes, CancellationToken cancellationToken = default);

    /// <summary>Extract, chunk, embed and store a registered document. Safe to re-run.</summary>
    Task<KnowledgeDocument> IndexAsync(Guid documentId, CancellationToken cancellationToken = default);
}
