namespace TaxNavi.Domain.Models;

/// <summary>One turn of an LLM conversation ("user" or "model").</summary>
public record LlmMessage(string Role, string Text)
{
    public const string UserRole = "user";
    public const string ModelRole = "model";
}

/// <summary>Provider-agnostic completion request.</summary>
public class LlmRequest
{
    public string SystemInstruction { get; set; } = string.Empty;
    public List<LlmMessage> Messages { get; set; } = new();
    public double Temperature { get; set; } = 0.2;
    public int MaxOutputTokens { get; set; } = 1024;
}

/// <summary>Provider-agnostic completion result with token accounting.</summary>
public class LlmResponse
{
    public bool Success { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public string? FinishReason { get; set; }

    /// <summary>Safe, generic error label when Success is false (never provider payloads).</summary>
    public string? Error { get; set; }
}
