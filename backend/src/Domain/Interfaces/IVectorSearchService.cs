using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Models;

namespace TaxNavi.Domain.Interfaces;

/// <summary>Stores chunk embeddings and runs semantic similarity search over them.</summary>
public interface IVectorSearchService
{
    /// <summary>Replace all chunks of a document (used on index and re-index).</summary>
    Task ReplaceDocumentChunksAsync(Guid documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nearest chunks to the query embedding by cosine similarity, best first.
    /// Only chunks of documents with status Indexed are searched.
    /// </summary>
    Task<List<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        double minSimilarity,
        CancellationToken cancellationToken = default);
}
