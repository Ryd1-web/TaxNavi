using TaxNavi.Domain.Entities;

namespace TaxNavi.Domain.Models;

/// <summary>A vector-search hit: the chunk, its parent document and the cosine similarity (0–1).</summary>
public record RetrievedChunk(DocumentChunk Chunk, KnowledgeDocument Document, double Similarity);
