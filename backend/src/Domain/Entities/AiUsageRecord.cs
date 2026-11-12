namespace TaxNavi.Domain.Entities;

/// <summary>
/// One AI operation (chat turn, report explanation, query embedding or ingestion run),
/// recorded for cost tracking, quota decisions and observability. Never stores question
/// or answer text — only counts, timings and categories.
/// </summary>
public class AiUsageRecord
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }

    /// <summary>Stable non-reversible key for anonymous callers (hashed client address).</summary>
    public string? ClientKey { get; set; }

    /// <summary>One of <see cref="AiOperations"/>.</summary>
    public string Operation { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int RetrievedChunks { get; set; }
    public double DurationMs { get; set; }
    public bool Success { get; set; }

    /// <summary>Coarse failure category ("provider", "timeout", "quota", …); never an exception message with user data.</summary>
    public string? ErrorCategory { get; set; }

    public Guid? ConversationId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class AiOperations
{
    public const string Chat = "chat";
    public const string ExplainReport = "explain-report";
    public const string Ingest = "ingest";
}
