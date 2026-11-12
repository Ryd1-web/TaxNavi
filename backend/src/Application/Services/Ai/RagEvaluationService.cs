using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Entities;

namespace TaxNavi.Application.Services;

/// <summary>One case of the repeatable evaluation set (knowledge-base/eval.json).</summary>
public class RagEvalCase
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public List<string> ExpectedDocuments { get; set; } = new();
    public List<string> ExpectedPhrases { get; set; } = new();
    public bool ExpectAnswerable { get; set; } = true;
    public string? ExpectGrounding { get; set; }
    public List<string> ForbiddenAnswerPhrases { get; set; } = new();
}

public class RagEvalCaseResult
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public int RetrievedChunks { get; set; }
    public double TopSimilarity { get; set; }
    public double RetrievalMs { get; set; }
    public bool? DocumentHit { get; set; }
    public bool? PhraseHit { get; set; }
    public List<string> RetrievedReferences { get; set; } = new();

    // Answer checks (only when answers are generated)
    public string? Grounding { get; set; }
    public string? Confidence { get; set; }
    public bool? GroundingAsExpected { get; set; }
    public bool? CitationsValid { get; set; }
    public bool? ForbiddenPhraseFound { get; set; }
    public int? CitationCount { get; set; }
    public double? AnswerMs { get; set; }
    public string? AnswerPreview { get; set; }
}

public class RagEvalReport
{
    public DateTime RanAt { get; set; }
    public int Cases { get; set; }
    public bool AnswersGenerated { get; set; }
    public double DocumentHitRate { get; set; }
    public double PhraseHitRate { get; set; }
    public double MeanTopSimilarity { get; set; }
    public double RetrievalP50Ms { get; set; }
    public double RetrievalP95Ms { get; set; }
    public double? GroundingAccuracy { get; set; }
    public double? CitationValidityRate { get; set; }
    public int? ForbiddenPhraseViolations { get; set; }
    public double? AnswerP50Ms { get; set; }
    public List<RagEvalCaseResult> Results { get; set; } = new();
}

/// <summary>
/// Repeatable, mostly-free evaluation of the RAG pipeline: retrieval precision proxies
/// (did the expected Act / phrase come back, how similar, how fast) for every case, and —
/// when <c>generateAnswers</c> is on — answer checks: expected grounding (answered vs
/// insufficient vs escalation), citation validity (no [S#] outside the supplied sources)
/// and prompt-injection resistance (forbidden phrases).
/// </summary>
public partial class RagEvaluationService
{
    private readonly IRetrievalService _retrieval;
    private readonly IAiAssistantService _assistant;
    private readonly TimeProvider _clock;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    [GeneratedRegex(@"\[S(\d{1,2})\]")]
    private static partial Regex Marker();

    public RagEvaluationService(IRetrievalService retrieval, IAiAssistantService assistant, TimeProvider clock)
    {
        _retrieval = retrieval;
        _assistant = assistant;
        _clock = clock;
    }

    public static List<RagEvalCase> LoadCases(Stream json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        return document.RootElement.TryGetProperty("cases", out var cases)
            ? cases.Deserialize<List<RagEvalCase>>(JsonOptions) ?? new List<RagEvalCase>()
            : new List<RagEvalCase>();
    }

    public async Task<RagEvalReport> RunAsync(IReadOnlyList<RagEvalCase> cases, bool generateAnswers, CancellationToken cancellationToken = default)
    {
        var results = new List<RagEvalCaseResult>();
        foreach (var @case in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = new RagEvalCaseResult { Id = @case.Id, Question = @case.Question };

            var sw = Stopwatch.StartNew();
            var hits = await _retrieval.RetrieveAsync(@case.Question, cancellationToken);
            sw.Stop();
            result.RetrievalMs = sw.Elapsed.TotalMilliseconds;
            result.RetrievedChunks = hits.Count;
            result.TopSimilarity = hits.Count > 0 ? Math.Round(hits[0].Similarity, 3) : 0;
            result.RetrievedReferences = hits.Select(h => $"{h.Document.Title} §{h.Chunk.SectionNumber ?? "-"} (p.{h.Chunk.PageFrom}, {h.Similarity:F2})").ToList();

            if (@case.ExpectedDocuments.Count > 0)
            {
                result.DocumentHit = hits.Any(h => @case.ExpectedDocuments.Any(d => h.Document.Title.Contains(d, StringComparison.OrdinalIgnoreCase)));
            }

            if (@case.ExpectedPhrases.Count > 0)
            {
                result.PhraseHit = hits.Any(h => @case.ExpectedPhrases.Any(p => h.Chunk.Content.Contains(p, StringComparison.OrdinalIgnoreCase)));
            }

            if (generateAnswers)
            {
                sw.Restart();
                var answer = await _assistant.AnswerOnceAsync(@case.Question, cancellationToken);
                sw.Stop();
                result.AnswerMs = sw.Elapsed.TotalMilliseconds;
                result.Grounding = answer.Grounding;
                result.Confidence = answer.Confidence;
                result.CitationCount = answer.Citations.Count;
                result.AnswerPreview = answer.Answer.Length > 240 ? answer.Answer[..240] + "…" : answer.Answer;

                var expectedGrounding = @case.ExpectGrounding
                    ?? (@case.ExpectAnswerable ? AnswerGroundings.Retrieval : null);
                result.GroundingAsExpected = expectedGrounding != null
                    ? answer.Grounding == expectedGrounding
                    : answer.Grounding != AnswerGroundings.Retrieval; // "must not answer as if grounded"

                var markers = Marker().Matches(answer.Answer).Select(m => int.Parse(m.Groups[1].Value)).ToList();
                result.CitationsValid = markers.All(n => n >= 1 && n <= hits.Count);
                result.ForbiddenPhraseFound = @case.ForbiddenAnswerPhrases.Any(p => answer.Answer.Contains(p, StringComparison.OrdinalIgnoreCase));
            }

            results.Add(result);
        }

        var retrievalTimes = results.Select(r => r.RetrievalMs).OrderBy(x => x).ToList();
        var report = new RagEvalReport
        {
            RanAt = _clock.GetUtcNow().UtcDateTime,
            Cases = results.Count,
            AnswersGenerated = generateAnswers,
            DocumentHitRate = Rate(results.Where(r => r.DocumentHit.HasValue).Select(r => r.DocumentHit!.Value)),
            PhraseHitRate = Rate(results.Where(r => r.PhraseHit.HasValue).Select(r => r.PhraseHit!.Value)),
            MeanTopSimilarity = results.Count > 0 ? Math.Round(results.Average(r => r.TopSimilarity), 3) : 0,
            RetrievalP50Ms = Percentile(retrievalTimes, 0.50),
            RetrievalP95Ms = Percentile(retrievalTimes, 0.95),
            Results = results
        };

        if (generateAnswers)
        {
            report.GroundingAccuracy = Rate(results.Where(r => r.GroundingAsExpected.HasValue).Select(r => r.GroundingAsExpected!.Value));
            report.CitationValidityRate = Rate(results.Where(r => r.CitationsValid.HasValue).Select(r => r.CitationsValid!.Value));
            report.ForbiddenPhraseViolations = results.Count(r => r.ForbiddenPhraseFound == true);
            report.AnswerP50Ms = Percentile(results.Where(r => r.AnswerMs.HasValue).Select(r => r.AnswerMs!.Value).OrderBy(x => x).ToList(), 0.50);
        }

        return report;
    }

    private static double Rate(IEnumerable<bool> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0 : Math.Round(list.Count(v => v) / (double)list.Count, 3);
    }

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        var index = (int)Math.Ceiling(p * sorted.Count) - 1;
        return Math.Round(sorted[Math.Clamp(index, 0, sorted.Count - 1)], 1);
    }
}
