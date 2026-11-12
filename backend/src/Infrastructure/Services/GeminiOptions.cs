namespace TaxNavi.Infrastructure.Services;

/// <summary>
/// Google Gemini configuration. Nothing here is hardcoded in services: models, sizes and
/// limits all come from configuration ("Gemini" section) or the GEMINI_API_KEY variable.
/// </summary>
public class GeminiOptions
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>Chat model for assistant answers and report explanations.</summary>
    public string ChatModel { get; set; } = "gemini-2.5-flash";

    /// <summary>Embedding model for retrieval.</summary>
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";

    /// <summary>
    /// Thinking-token budget for chat calls. 0 disables thinking on models that allow it
    /// (cheapest); null omits the field for models that configure thinking differently.
    /// </summary>
    public int? ThinkingBudget { get; set; } = 0;

    public int MaxOutputTokens { get; set; } = 1200;
    public double Temperature { get; set; } = 0.2;

    /// <summary>Chunk texts per embedding batch request.</summary>
    public int EmbeddingBatchSize { get; set; } = 20;

    /// <summary>Pause between embedding batches; keeps first-time indexing under free-tier per-minute limits.</summary>
    public int EmbeddingBatchDelayMs { get; set; } = 1500;

    /// <summary>Retries for 429/5xx before giving up.</summary>
    public int MaxRetries { get; set; } = 4;

    /// <summary>Longest server-suggested wait honoured inline for chat (users are waiting).</summary>
    public int MaxChatRetryWaitSeconds { get; set; } = 8;

    /// <summary>Longest wait honoured during background indexing (nobody is waiting).</summary>
    public int MaxIndexRetryWaitSeconds { get; set; } = 90;

    /// <summary>After a quota rejection, skip Gemini app-wide for this long (rules assistant answers meanwhile).</summary>
    public int QuotaCooldownMinutes { get; set; } = 10;
}
