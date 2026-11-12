namespace TaxNavi.Domain.Interfaces;

/// <summary>
/// Turns text into fixed-size vectors. Queries and documents are embedded with different
/// task hints because retrieval models are trained asymmetrically. Vectors are L2-normalised
/// so cosine similarity is safe regardless of provider.
/// </summary>
public interface IEmbeddingService
{
    bool IsConfigured { get; }
    string ModelName { get; }

    /// <summary>Embed a user question (RETRIEVAL_QUERY task).</summary>
    Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Embed document chunks in provider-sized batches (RETRIEVAL_DOCUMENT task).</summary>
    Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
