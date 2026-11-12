using System.Text;
using System.Text.RegularExpressions;

namespace TaxNavi.Infrastructure.Services;

/// <summary>
/// Structure-aware chunking for Nigerian legislation and official guidance.
///
/// Legislation (Official Gazette layout) is split on its own structure — CHAPTER / PART / SCHEDULE
/// headings and numbered sections ("12.—(1) …") — so a chunk is a whole section wherever possible and
/// always knows which chapter, part, section and pages it came from. The "Arrangement of Sections"
/// block at the front is not indexed as content, but its entries supply the official title of each
/// section. Section numbering must run sequentially (restarting only after a SCHEDULE heading), which
/// keeps list items and cross-references from being mistaken for section starts. Oversized sections
/// are split at subsection boundaries with overlap; each piece keeps the same citation.
/// Documents without that structure (guidance, circulars) fall back to a paragraph sliding window.
/// </summary>
public static partial class LegislationChunker
{
    /// <summary>Target chunk budget in approximate tokens (chars / 4).</summary>
    public const int TargetChunkTokens = 700;

    /// <summary>Overlap carried between consecutive pieces of a split section or window.</summary>
    public const int OverlapTokens = 90;

    private const int TargetChunkChars = TargetChunkTokens * 4;
    private const int OverlapChars = OverlapTokens * 4;
    private const int MinChunkChars = 120;
    private const int MaxHeadingChars = 120;

    private const string Ordinal = "[IVXLC]+|\\d{1,3}|ONE|TWO|THREE|FOUR|FIVE|SIX|SEVEN|EIGHT|NINE|TEN|ELEVEN|TWELVE";

    // Headings are printed in capitals in the gazette; body prose ("part of", "First Schedule to…") is not.
    [GeneratedRegex($@"^\s*CHAPTER\s+({Ordinal})\b[\s\p{{Pd}}:]*(.*)$")]
    private static partial Regex ChapterHeading();

    [GeneratedRegex($@"^\s*PART\s+({Ordinal})\b[\s\p{{Pd}}:]*(.*)$")]
    private static partial Regex PartHeading();

    [GeneratedRegex(@"^\s*((?:FIRST|SECOND|THIRD|FOURTH|FIFTH|SIXTH|SEVENTH|EIGHTH|NINTH|TENTH|ELEVENTH|TWELFTH|THIRTEENTH|FOURTEENTH|FIFTEENTH)\s+SCHEDULE)\b(.*)$")]
    private static partial Regex ScheduleHeading();

    // "12.—(1) …", "12. — (1) …", "12. Imposition of tax". Optional ALL-CAPS prefix = tail of a wrapped heading
    // that the extractor merged onto the section line ("AND INCOME 22.—(1) …").
    [GeneratedRegex(@"^\s*(?:(?<prefix>[A-Z][A-Z ,'&()\p{Pd}]{2,80}?)\s+)?(?<num>\d{1,3})\s*\.\s*(?:\p{Pd}+\s*)?(?<rest>.*)$")]
    private static partial Regex SectionStart();

    [GeneratedRegex(@"(?=\n\s*\(\d{1,2}\)\s)")]
    private static partial Regex SubsectionBoundary();

    [GeneratedRegex(@"\bARRANGEMENT\s+OF\s+SECTIONS\b", RegexOptions.IgnoreCase)] // printed mid-line after the page header, sometimes double-spaced
    private static partial Regex ArrangementStart();

    // Enacting formula (the gazette's own spelling varies: ENACTED / ENECTED), long title, or commencement date.
    [GeneratedRegex(@"^\s*((BE IT )?EN[AE]CTED\b|AN ACT TO\b|\[\d{1,2}(st|nd|rd|th)?\s+[A-Z][a-z]+,?\s+\d{4}\])", RegexOptions.IgnoreCase)]
    private static partial Regex ArrangementEnd();

    public static List<ChunkDraft> Chunk(IReadOnlyList<PdfPageText> pages, bool isLegislation)
    {
        return isLegislation ? ChunkLegislation(pages) : ChunkByParagraphs(pages);
    }

    private static List<ChunkDraft> ChunkLegislation(IReadOnlyList<PdfPageText> pages)
    {
        var chunks = new List<ChunkDraft>();
        var titles = new Dictionary<int, string>();
        string? chapter = null, part = null;
        ChunkDraft? current = null;
        var buffer = new StringBuilder();
        var lastSection = 0;
        var inSchedules = false;

        // Gazettes open with front matter (cover, arrangement of sections) that ends at the long title /
        // enacting formula. The extractor can emit arrangement columns out of order — even before the
        // "ARRANGEMENT OF SECTIONS" line — so when both markers exist, front matter runs from page 1.
        var frontMatterText = string.Join("\n", pages.Take(15).Select(p => p.Text));
        var inArrangement = ArrangementStart().IsMatch(frontMatterText) &&
                            frontMatterText.Split('\n').Any(l => ArrangementEnd().IsMatch(l));
        int? lastArrangementEntry = null;
        var harvestTitles = true;

        void Flush()
        {
            if (current == null)
            {
                return;
            }

            var text = buffer.ToString().Trim();
            buffer.Clear();
            if (text.Length >= MinChunkChars)
            {
                current.Content = text;
                chunks.AddRange(SplitIfOversized(current));
            }

            current = null;
        }

        foreach (var page in pages)
        {
            foreach (var rawLine in page.Text.Split('\n'))
            {
                var line = rawLine.TrimEnd();
                if (line.Length == 0)
                {
                    buffer.AppendLine();
                    continue;
                }

                // ---- Arrangement of sections: harvest titles, index nothing ----
                if (ArrangementStart().IsMatch(line))
                {
                    Flush();
                    inArrangement = true;
                    continue;
                }

                if (inArrangement)
                {
                    if (ArrangementEnd().IsMatch(line))
                    {
                        inArrangement = false;
                        lastSection = 0;
                        chapter = part = null;
                        continue;
                    }

                    // The listing is harvested order-tolerantly (gazette columns come out of the extractor
                    // jumbled); only the main body listing carries titles — stop at the SCHEDULES line.
                    if (line.Trim().Equals("SCHEDULES", StringComparison.OrdinalIgnoreCase))
                    {
                        harvestTitles = false;
                        continue;
                    }

                    var entry = SectionStart().Match(line);
                    if (harvestTitles && entry.Success && entry.Groups["prefix"].Length == 0 &&
                        int.TryParse(entry.Groups["num"].Value, out var n) && n > 0 && LooksLikeTitle(entry.Groups["rest"].Value))
                    {
                        titles.TryAdd(n, Clean(entry.Groups["rest"].Value));
                        lastArrangementEntry = n;
                    }
                    else if (harvestTitles && lastArrangementEntry is { } prev && titles.TryGetValue(prev, out var title) &&
                             title.Length < 90 && !IsTocHeadingLike(line) && LooksLikeTitle(line))
                    {
                        titles[prev] = title + " " + Clean(line); // wrapped title line
                        lastArrangementEntry = null;
                    }
                    else
                    {
                        lastArrangementEntry = null;
                    }

                    continue;
                }

                // ---- Structural headings (uppercase only) ----
                if (line.Length <= MaxHeadingChars)
                {
                    if (ChapterHeading().Match(line) is { Success: true } ch)
                    {
                        Flush();
                        chapter = $"Chapter {ch.Groups[1].Value.Trim()}" + Title(ch.Groups[2].Value);
                        part = null;
                        continue;
                    }

                    if (PartHeading().Match(line) is { Success: true } pt)
                    {
                        Flush();
                        part = $"Part {pt.Groups[1].Value.Trim()}" + Title(pt.Groups[2].Value);
                        continue;
                    }

                    if (ScheduleHeading().Match(line) is { Success: true } sch)
                    {
                        Flush();
                        part = Capitalize(sch.Groups[1].Value.Trim()) + Title(sch.Groups[2].Value);
                        inSchedules = true;
                        lastSection = 0; // schedule paragraphs restart at 1
                        continue;
                    }
                }

                // ---- Section starts ----
                var section = SectionStart().Match(line);
                if (section.Success && int.TryParse(section.Groups["num"].Value, out var number) && IsNextSection(number, lastSection))
                {
                    Flush();
                    var prefix = section.Groups["prefix"].Value.Trim();
                    if (prefix.Length > 0 && part != null && !part.EndsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        part += " " + Capitalize(prefix); // wrapped heading tail merged onto this line
                    }

                    lastSection = number;
                    current = new ChunkDraft
                    {
                        Chapter = chapter,
                        Part = part,
                        SectionNumber = number.ToString(),
                        SectionTitle = inSchedules ? null : titles.GetValueOrDefault(number) ?? InlineTitle(section.Groups["rest"].Value),
                        PageFrom = page.PageNumber,
                        PageTo = page.PageNumber
                    };
                    buffer.AppendLine(prefix.Length > 0 ? line.Trim()[prefix.Length..].TrimStart() : line.Trim());
                    continue;
                }

                if (current == null)
                {
                    // Preamble (long title, commencement) and un-numbered text stay retrievable.
                    current = new ChunkDraft { Chapter = chapter, Part = part, PageFrom = page.PageNumber, PageTo = page.PageNumber };
                }

                current.PageTo = page.PageNumber;
                buffer.AppendLine(line.Trim());
            }
        }

        Flush();
        return chunks.Count > 0 ? chunks : ChunkByParagraphs(pages);
    }

    /// <summary>Sections run sequentially; a small gap tolerates a start the extractor mangled.</summary>
    private static bool IsNextSection(int number, int last) => number > last && number - last <= 3;

    /// <summary>In the arrangement block, headings may be mixed case ("Part X – Development Levy").</summary>
    private static bool IsTocHeadingLike(string line)
    {
        var t = line.Trim();
        return t.StartsWith("PART ", StringComparison.OrdinalIgnoreCase) ||
               t.StartsWith("CHAPTER ", StringComparison.OrdinalIgnoreCase) ||
               t.Contains("SCHEDULE", StringComparison.OrdinalIgnoreCase) ||
               t.StartsWith("Nigeria Tax", StringComparison.OrdinalIgnoreCase) || // running page header
               t.Equals("SCHEDULES", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Arrangement entries are short noun phrases, not sentences.</summary>
    private static bool LooksLikeTitle(string text)
    {
        var t = Clean(text);
        return t.Length is >= 3 and <= 120 && !t.EndsWith('.') && !t.EndsWith(';') && t.Split(' ').Length <= 16 &&
               !t.Contains(".. ..") && !t.Contains("Printed", StringComparison.OrdinalIgnoreCase); // gazette cover / page furniture
    }

    private static IEnumerable<ChunkDraft> SplitIfOversized(ChunkDraft draft)
    {
        if (draft.Content.Length <= TargetChunkChars + TargetChunkChars / 2)
        {
            yield return draft;
            yield break;
        }

        var parts = SubsectionBoundary().Split(draft.Content)
            .SelectMany(p => p.Length > TargetChunkChars * 2 ? HardWrap(p) : new[] { p })
            .Where(p => p.Trim().Length > 0)
            .ToList();

        var piece = new StringBuilder();
        string overlapTail = string.Empty;
        var index = 0;
        foreach (var part in parts)
        {
            if (piece.Length > 0 && piece.Length + part.Length > TargetChunkChars)
            {
                yield return Clone(draft, Decorate(piece.ToString(), draft, index++, overlapTail));
                overlapTail = Tail(piece.ToString(), OverlapChars);
                piece.Clear();
            }

            piece.Append(part);
        }

        if (piece.ToString().Trim().Length >= MinChunkChars || index == 0)
        {
            yield return Clone(draft, Decorate(piece.ToString(), draft, index, overlapTail));
        }
    }

    private static string Decorate(string text, ChunkDraft draft, int index, string overlapTail)
    {
        var body = text.Trim();
        if (index == 0)
        {
            return body;
        }

        // Continuation pieces restate where they belong and carry a little preceding text,
        // so the passage still reads sensibly on its own.
        var header = draft.SectionNumber != null ? $"[Section {draft.SectionNumber} — continued]" : "[continued]";
        return string.IsNullOrEmpty(overlapTail) ? $"{header}\n{body}" : $"{header}\n… {overlapTail}\n{body}";
    }

    private static List<ChunkDraft> ChunkByParagraphs(IReadOnlyList<PdfPageText> pages)
    {
        var chunks = new List<ChunkDraft>();
        var piece = new StringBuilder();
        int pageFrom = pages.Count > 0 ? pages[0].PageNumber : 1, pageTo = pageFrom;
        string overlapTail = string.Empty;

        void Flush()
        {
            var text = piece.ToString().Trim();
            piece.Clear();
            if (text.Length < MinChunkChars)
            {
                return;
            }

            var content = string.IsNullOrEmpty(overlapTail) ? text : $"… {overlapTail}\n{text}";
            chunks.Add(new ChunkDraft { Content = content, PageFrom = pageFrom, PageTo = pageTo });
            overlapTail = Tail(text, OverlapChars);
        }

        foreach (var page in pages)
        {
            foreach (var paragraph in page.Text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
                         .SelectMany(p => p.Length > TargetChunkChars * 2 ? HardWrap(p) : new[] { p }))
            {
                if (piece.Length > 0 && piece.Length + paragraph.Length > TargetChunkChars)
                {
                    Flush();
                    pageFrom = page.PageNumber;
                }

                pageTo = page.PageNumber;
                piece.AppendLine(paragraph.Trim());
                piece.AppendLine();
            }
        }

        Flush();
        return chunks;
    }

    /// <summary>Last-resort split for pathological unbroken text, at sentence ends where possible.</summary>
    private static IEnumerable<string> HardWrap(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(TargetChunkChars, text.Length - start);
            if (start + length < text.Length)
            {
                var window = text.AsSpan(start, length);
                var cut = window.LastIndexOfAny('.', ';');
                if (cut > length / 2)
                {
                    length = cut + 1;
                }
            }

            yield return text.Substring(start, length);
            start += length;
        }
    }

    private static ChunkDraft Clone(ChunkDraft source, string content) => new()
    {
        Content = content,
        Chapter = source.Chapter,
        Part = source.Part,
        SectionNumber = source.SectionNumber,
        SectionTitle = source.SectionTitle,
        PageFrom = source.PageFrom,
        PageTo = source.PageTo
    };

    private static string Tail(string text, int chars)
    {
        text = text.Trim();
        if (text.Length <= chars)
        {
            return text;
        }

        var tail = text[^chars..];
        var firstSpace = tail.IndexOf(' ');
        return firstSpace > 0 ? tail[(firstSpace + 1)..] : tail;
    }

    /// <summary>
    /// A short heading printed on the section line itself ("2. Interpretation"). Prose openings
    /// ("1. The objective of this Act is…") are not titles and return null.
    /// </summary>
    private static string? InlineTitle(string remainder)
    {
        var title = Clean(remainder);
        if (title.Length < 3 || title.Length > 70 || title.StartsWith('(') || title.EndsWith('.') ||
            title.Split(' ').Length > 9 || char.IsLower(title[0]))
        {
            return null;
        }

        return title;
    }

    private static string Clean(string value) => Regex.Replace(value, @"\s+", " ").Trim().TrimEnd('.', ' ');

    private static string Title(string remainder)
    {
        var value = Clean(remainder);
        return value.Length is > 2 and < 100 ? $" — {Capitalize(value)}" : string.Empty;
    }

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}
