namespace TaxNavi.Domain.Entities;

/// <summary>One message in an assistant conversation, with the evidence behind an answer.</summary>
public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public ChatConversation Conversation { get; set; } = null!;

    /// <summary>"user" or "assistant".</summary>
    public string Role { get; set; } = ChatMessageRoles.User;

    public string Content { get; set; } = string.Empty;

    /// <summary>Strictly increasing within a conversation; message order never depends on timestamps.</summary>
    public int Ordinal { get; set; }

    /// <summary>JSON array of citations (document, section, pages, score) for assistant messages.</summary>
    public string? CitationsJson { get; set; }

    /// <summary>How the answer was produced: one of <see cref="AnswerGroundings"/>.</summary>
    public string? Grounding { get; set; }

    public string? Model { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class ChatMessageRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

public static class AnswerGroundings
{
    /// <summary>Answered by the LLM from retrieved legislation/guidance.</summary>
    public const string Retrieval = "retrieval";

    /// <summary>Answered deterministically from the versioned rule set (no LLM).</summary>
    public const string Rules = "rules";

    /// <summary>Routed to professional advice (no LLM).</summary>
    public const string Escalation = "escalation";

    /// <summary>The model judged the retrieved sources insufficient to answer confidently.</summary>
    public const string InsufficientEvidence = "insufficient";
}
