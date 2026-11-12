using System.Text.RegularExpressions;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Models;

namespace TaxNavi.Application.Services;

/// <summary>Shared citation helpers: parse [S#] markers and map retrieved chunks to citation DTOs.</summary>
public static partial class RagCitations
{
    [GeneratedRegex(@"\[S(\d{1,2})\]")]
    private static partial Regex Marker();

    /// <summary>Distinct, in-range marker numbers found in the answer text, ascending.</summary>
    public static List<int> ParseMarkers(string text, int sourceCount) =>
        Marker().Matches(text)
            .Select(m => int.Parse(m.Groups[1].Value))
            .Where(n => n >= 1 && n <= sourceCount)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

    public static AiCitationDto ToCitation(RetrievedChunk hit, int number) => new()
    {
        Reference = $"S{number}",
        DocumentTitle = hit.Document.Title,
        DocumentType = hit.Document.DocumentType,
        SectionNumber = hit.Chunk.SectionNumber,
        SectionTitle = hit.Chunk.SectionTitle,
        Chapter = hit.Chunk.Chapter,
        Part = hit.Chunk.Part,
        PageFrom = hit.Chunk.PageFrom,
        PageTo = hit.Chunk.PageTo,
        SourceName = hit.Document.SourceName,
        SourceUrl = hit.Document.SourceUrl,
        Similarity = Math.Round(hit.Similarity, 3),
        Snippet = hit.Chunk.Content.Length > 280 ? hit.Chunk.Content[..280] + "…" : hit.Chunk.Content
    };
}
