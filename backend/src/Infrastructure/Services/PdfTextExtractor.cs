using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace TaxNavi.Infrastructure.Services;

/// <summary>Text of one PDF page, 1-based page number preserved for citations.</summary>
public record PdfPageText(int PageNumber, string Text);

/// <summary>Extracts per-page text from a PDF with PdfPig (pure managed code, no native deps).</summary>
public static class PdfTextExtractor
{
    public static List<PdfPageText> Extract(byte[] pdfBytes)
    {
        var pages = new List<PdfPageText>();
        using var document = PdfDocument.Open(pdfBytes);
        foreach (var page in document.GetPages())
        {
            // ContentOrderTextExtractor keeps reading order and word spacing far better than page.Text.
            pages.Add(new PdfPageText(page.Number, ContentOrderTextExtractor.GetText(page)));
        }

        return pages;
    }
}
