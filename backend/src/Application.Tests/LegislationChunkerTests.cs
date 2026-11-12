using TaxNavi.Infrastructure.Services;
using Xunit;

namespace TaxNavi.Application.Tests;

public class LegislationChunkerTests
{
    private static string Sentence(string lead, int repeat = 6) =>
        lead + " " + string.Join(" ", Enumerable.Repeat("The provisions of this subsection apply to every taxable person in Nigeria.", repeat));

    [Fact]
    public void Legislation_SplitsOnSections_AndKeepsStructure()
    {
        var pages = new List<PdfPageText>
        {
            new(1, $"""
                CHAPTER ONE
                PART I — PRELIMINARY PROVISIONS
                1.—(1) {Sentence("This Act may be cited as the Test Tax Act.")}
                2. Interpretation
                (1) {Sentence("In this Act, unless the context otherwise requires—")}
                """),
            new(2, $"""
                PART II — IMPOSITION OF TAX
                3.—(1) {Sentence("Tax is imposed on the income of every person.")}
                """)
        };

        var chunks = LegislationChunker.Chunk(pages, isLegislation: true);

        Assert.Equal(3, chunks.Count);
        Assert.Equal("1", chunks[0].SectionNumber);
        Assert.Contains("Part I", chunks[0].Part);
        Assert.Contains("Chapter ONE", chunks[0].Chapter);
        Assert.Equal(1, chunks[0].PageFrom);

        Assert.Equal("2", chunks[1].SectionNumber);
        Assert.Equal("Interpretation", chunks[1].SectionTitle);

        Assert.Equal("3", chunks[2].SectionNumber);
        Assert.Contains("Part II", chunks[2].Part);
        Assert.Equal(2, chunks[2].PageFrom);
    }

    [Fact]
    public void Legislation_SectionSpanningPages_TracksPageRange()
    {
        var pages = new List<PdfPageText>
        {
            new(4, $"1.—(1) {Sentence("First page of the section.")}"),
            new(5, Sentence("It continues on the following page without a new heading."))
        };

        var chunks = LegislationChunker.Chunk(pages, isLegislation: true);

        var section = Assert.Single(chunks);
        Assert.Equal(4, section.PageFrom);
        Assert.Equal(5, section.PageTo);
    }

    [Fact]
    public void OversizedSection_IsSplitAtSubsections_WithContinuationContext()
    {
        var subsections = string.Join("\n", Enumerable.Range(1, 12)
            .Select(n => $"({n}) {Sentence($"Subsection {n} applies.", 8)}"));
        var pages = new List<PdfPageText> { new(1, $"1.—{subsections}") };

        var chunks = LegislationChunker.Chunk(pages, isLegislation: true);

        Assert.True(chunks.Count > 1, "an oversized section should be split");
        Assert.All(chunks, c => Assert.Equal("1", c.SectionNumber));
        Assert.Contains("[Section 1 — continued]", chunks[1].Content);
        Assert.Contains("…", chunks[1].Content); // carries overlap from the previous piece
    }

    [Fact]
    public void CrossReferences_DoNotStartNewSections()
    {
        // "45." deep inside section 2 text must not be treated as section 45.
        var pages = new List<PdfPageText>
        {
            new(1, $"""
                1.—(1) {Sentence("First section.")}
                2.—(1) {Sentence("Second section referring to other provisions.")}
                45. This is a list item inside section 2, not a real section heading.
                {Sentence("The section continues after the list item.")}
                """)
        };

        var chunks = LegislationChunker.Chunk(pages, isLegislation: true);

        Assert.Equal(2, chunks.Count);
        Assert.Contains("45.", chunks[1].Content);
    }

    [Fact]
    public void Guidance_FallsBackToParagraphWindows_WithOverlap()
    {
        var paragraphs = string.Join("\n\n", Enumerable.Range(1, 10)
            .Select(n => Sentence($"Guidance paragraph {n}.", 5)));
        var pages = new List<PdfPageText> { new(1, paragraphs) };

        var chunks = LegislationChunker.Chunk(pages, isLegislation: false);

        Assert.True(chunks.Count > 1);
        Assert.Null(chunks[0].SectionNumber);
        Assert.StartsWith("…", chunks[1].Content); // sliding-window overlap
    }

    [Fact]
    public void ScannedOrEmptyPdf_YieldsNoChunks()
    {
        var chunks = LegislationChunker.Chunk(new List<PdfPageText> { new(1, "  \n ") }, isLegislation: true);
        Assert.Empty(chunks);
    }
}
