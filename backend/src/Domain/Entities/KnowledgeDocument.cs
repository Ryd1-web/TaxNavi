namespace TaxNavi.Domain.Entities;

/// <summary>
/// A source document in the AI knowledge base (an Act, regulation or official guidance PDF).
/// The original file is kept so chunks can be rebuilt without re-uploading, and versions are
/// never silently replaced: a newer version points at the document it supersedes.
/// </summary>
public class KnowledgeDocument
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>One of <see cref="KnowledgeDocumentTypes"/> — binding legislation vs explanatory guidance.</summary>
    public string DocumentType { get; set; } = KnowledgeDocumentTypes.Legislation;

    /// <summary>Publisher, e.g. "Nigeria Revenue Service" or "National Assembly".</summary>
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime? PublicationDate { get; set; }
    public DateTime? EffectiveDate { get; set; }

    /// <summary>First tax year the document applies to, when known (e.g. 2026 for NTA 2025).</summary>
    public int? TaxYear { get; set; }

    /// <summary>Monotonic version per title; a re-ingested amendment gets Version + 1.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The document this version replaces, if any. The old row is kept for audit.</summary>
    public Guid? SupersedesDocumentId { get; set; }

    /// <summary>SHA-256 of the original file; unique, so the same PDF is never indexed twice.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>One of <see cref="KnowledgeDocumentStatuses"/>.</summary>
    public string Status { get; set; } = KnowledgeDocumentStatuses.Pending;
    public string? LastError { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>Original PDF bytes. Kept in the database because the API host has no durable disk.</summary>
    public byte[] FileContent { get; set; } = Array.Empty<byte>();

    public int PageCount { get; set; }
    public int ChunkCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? IndexedAt { get; set; }

    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}

public static class KnowledgeDocumentTypes
{
    /// <summary>Legally binding text (an Act or regulation).</summary>
    public const string Legislation = "Legislation";

    /// <summary>Official but explanatory material (circulars, guidelines, FAQs).</summary>
    public const string Guidance = "Guidance";
}

public static class KnowledgeDocumentStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Indexed = "Indexed";
    public const string Failed = "Failed";
    public const string Superseded = "Superseded";
}
