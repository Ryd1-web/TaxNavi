using Pgvector;

namespace TaxNavi.Domain.Entities;

/// <summary>
/// One retrievable passage of a knowledge document, with the structural context
/// (chapter / part / section) and page range needed for a verifiable citation.
/// </summary>
public class DocumentChunk
{
    /// <summary>Embedding size stored in the vector column. Changing this requires a migration + re-index.</summary>
    public const int EmbeddingDimensions = 1536;

    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public KnowledgeDocument Document { get; set; } = null!;

    /// <summary>Position of the chunk within the document (0-based, in reading order).</summary>
    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    // Structural context, preserved so a chunk keeps its legal meaning and citation.
    public string? Chapter { get; set; }
    public string? Part { get; set; }
    public string? SectionNumber { get; set; }
    public string? SectionTitle { get; set; }
    public int PageFrom { get; set; }
    public int PageTo { get; set; }

    /// <summary>Approximate token count of <see cref="Content"/> (chars / 4), used for context budgeting.</summary>
    public int TokenCount { get; set; }

    public Vector Embedding { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
