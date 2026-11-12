using System.Text;
using TaxNavi.Domain.Entities;

namespace TaxNavi.Domain.Models;

/// <summary>Shared formatting for a chunk's structural breadcrumb, used for embedding text and citations.</summary>
public static class ChunkFormat
{
    /// <summary>E.g. "Nigeria Tax Act 2025 › Part II › Section 12 — Imposition of tax (pp. 14–15)".</summary>
    public static string Breadcrumb(KnowledgeDocument document, DocumentChunk chunk, bool withPages = true)
    {
        var sb = new StringBuilder(document.Title);
        if (!string.IsNullOrWhiteSpace(chunk.Chapter))
        {
            sb.Append(" › ").Append(chunk.Chapter);
        }

        if (!string.IsNullOrWhiteSpace(chunk.Part))
        {
            sb.Append(" › ").Append(chunk.Part);
        }

        if (!string.IsNullOrWhiteSpace(chunk.SectionNumber))
        {
            sb.Append(" › Section ").Append(chunk.SectionNumber);
            if (!string.IsNullOrWhiteSpace(chunk.SectionTitle))
            {
                sb.Append(" — ").Append(chunk.SectionTitle);
            }
        }

        if (withPages && chunk.PageFrom > 0)
        {
            sb.Append(chunk.PageTo > chunk.PageFrom ? $" (pp. {chunk.PageFrom}–{chunk.PageTo})" : $" (p. {chunk.PageFrom})");
        }

        return sb.ToString();
    }

    /// <summary>Text that gets embedded: breadcrumb context + content, so retrieval sees the structure.</summary>
    public static string EmbeddingText(KnowledgeDocument document, DocumentChunk chunk) =>
        $"{Breadcrumb(document, chunk, withPages: false)} [{document.DocumentType}]\n\n{chunk.Content}";
}
