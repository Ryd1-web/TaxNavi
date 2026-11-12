namespace TaxNavi.Infrastructure.Services;

/// <summary>A chunk produced by the chunker before it is embedded and stored.</summary>
public class ChunkDraft
{
    public string Content { get; set; } = string.Empty;
    public string? Chapter { get; set; }
    public string? Part { get; set; }
    public string? SectionNumber { get; set; }
    public string? SectionTitle { get; set; }
    public int PageFrom { get; set; }
    public int PageTo { get; set; }
}
