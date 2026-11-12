namespace TaxNavi.Application.Services;

/// <summary>Retrieval and usage limits for the AI assistant ("Ai" configuration section).</summary>
public class AiOptions
{
    /// <summary>Chunks fetched per question before the context budget is applied.</summary>
    public int TopK { get; set; } = 6;

    /// <summary>Minimum cosine similarity (0–1) for a chunk to count as evidence.</summary>
    public double MinSimilarity { get; set; } = 0.35;

    /// <summary>Approximate token budget for retrieved context in one prompt.</summary>
    public int MaxContextTokens { get; set; } = 3200;

    /// <summary>How many past messages of a conversation are replayed to the model.</summary>
    public int MaxHistoryMessages { get; set; } = 10;

    /// <summary>Approximate token budget for replayed history.</summary>
    public int MaxHistoryTokens { get; set; } = 1600;

    public int MaxQuestionLength { get; set; } = 1500;

    /// <summary>Successful AI calls allowed per signed-in user per UTC day.</summary>
    public int DailyMessagesPerUser { get; set; } = 60;

    /// <summary>Successful AI calls allowed per anonymous client per UTC day.</summary>
    public int DailyMessagesPerGuest { get; set; } = 10;
}
