using Microsoft.Extensions.Logging;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;

namespace TaxNavi.Application.Services;

/// <summary>
/// The retrieval half of RAG: question → embedding → pgvector search → budget-trimmed,
/// similarity-ranked passages. Irrelevant chunks are dropped by the similarity threshold
/// rather than padded in to fill the context window.
/// </summary>
public class RetrievalService : IRetrievalService
{
    private readonly IEmbeddingService _embeddings;
    private readonly IVectorSearchService _vectors;
    private readonly AiOptions _options;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(IEmbeddingService embeddings, IVectorSearchService vectors, AiOptions options, ILogger<RetrievalService> logger)
    {
        _embeddings = embeddings;
        _vectors = vectors;
        _options = options;
        _logger = logger;
    }

    public async Task<List<RetrievedChunk>> RetrieveAsync(string query, CancellationToken cancellationToken = default)
    {
        if (!_embeddings.IsConfigured)
        {
            return new List<RetrievedChunk>();
        }

        var embedding = await _embeddings.EmbedQueryAsync(query, cancellationToken);
        var hits = await _vectors.SearchAsync(embedding, _options.TopK, _options.MinSimilarity, cancellationToken);

        // Enforce the context budget: keep the best chunks that fit.
        var kept = new List<RetrievedChunk>();
        var budget = _options.MaxContextTokens;
        foreach (var hit in hits)
        {
            var cost = Math.Max(hit.Chunk.TokenCount, hit.Chunk.Content.Length / 4);
            if (cost > budget && kept.Count > 0)
            {
                continue;
            }

            kept.Add(hit);
            budget -= cost;
            if (budget <= 0)
            {
                break;
            }
        }

        _logger.LogInformation("Retrieval: {Hits} hits, {Kept} kept, top similarity {Top:F3}",
            hits.Count, kept.Count, hits.Count > 0 ? hits[0].Similarity : 0);
        return kept;
    }
}
