using TaxNavi.Infrastructure.Services;
using Xunit;

namespace TaxNavi.Application.Tests;

/// <summary>
/// Runs the extractor + chunker over the real Official Gazette PDFs in knowledge-base/sources
/// (no network, no embeddings) and checks the legal structure survives. Skipped if the files
/// are absent from this checkout.
/// </summary>
public class RealCorpusChunkingTests
{
    private static string? Find(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "knowledge-base", "sources", file);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    [Fact]
    public void NigeriaTaxAct2025_ChunksBySection_WithPartsAndPages()
    {
        var path = Find("nigeria-tax-act-2025-gazette.pdf");
        if (path == null) return; // corpus not present

        var pages = PdfTextExtractor.Extract(File.ReadAllBytes(path));
        Assert.True(pages.Count >= 200, $"expected the full gazette, got {pages.Count} pages");

        var chunks = LegislationChunker.Chunk(pages, isLegislation: true);
        var sections = chunks.Where(c => c.SectionNumber != null).Select(c => int.Parse(c.SectionNumber!)).Distinct().ToList();

        Assert.True(sections.Count >= 150, $"NTA 2025 has 203 sections; only {sections.Count} were detected");
        Assert.Contains(chunks, c => c.Part != null && c.Part.Contains("Part II", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chunks, c => c.Part != null && c.Part.Contains("Schedule", StringComparison.OrdinalIgnoreCase));
        Assert.All(chunks, c => Assert.True(c.PageFrom >= 1 && c.PageTo >= c.PageFrom));

        // Budget discipline: no chunk wildly over target, nothing trivially small.
        var maxChars = LegislationChunker.TargetChunkTokens * 4 * 2;
        Assert.All(chunks, c => Assert.True(c.Content.Length <= maxChars, $"chunk of {c.Content.Length} chars exceeds budget"));
        Assert.True(chunks.Average(c => c.Content.Length) > 400, "chunks are suspiciously small");
    }

    [Fact]
    public void TaxAdministrationAct_IsExtractable()
    {
        var path = Find("nigeria-tax-administration-act-2025-gazette.pdf");
        if (path == null) return;

        var chunks = LegislationChunker.Chunk(PdfTextExtractor.Extract(File.ReadAllBytes(path)), isLegislation: true);
        Assert.True(chunks.Count(c => c.SectionNumber != null) >= 60);
    }
}
