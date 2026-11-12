namespace TaxNavi.Application.DTOs;

public class AiChatRequestDto
{
    public Guid? ConversationId { get; set; }

    /// <summary>Required to continue a guest conversation; issued when the conversation was created.</summary>
    public string? GuestToken { get; set; }

    public string Question { get; set; } = string.Empty;
    public int? TaxYear { get; set; }

    /// <summary>Optional calculator input — recomputed server-side, never trusted as numbers.</summary>
    public TaxCalculationRequestDto? Calculation { get; set; }
}

public class AiCitationDto
{
    /// <summary>Inline marker used in the answer text, e.g. "S1".</summary>
    public string Reference { get; set; } = string.Empty;

    public string DocumentTitle { get; set; } = string.Empty;

    /// <summary>"Legislation" (binding) or "Guidance" (explanatory).</summary>
    public string DocumentType { get; set; } = string.Empty;

    public string? SectionNumber { get; set; }
    public string? SectionTitle { get; set; }
    public string? Chapter { get; set; }
    public string? Part { get; set; }
    public int PageFrom { get; set; }
    public int PageTo { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public double Similarity { get; set; }

    /// <summary>Short quote of the passage behind the citation, for the expandable source view.</summary>
    public string Snippet { get; set; } = string.Empty;
}

public class AiChatResponseDto
{
    public Guid ConversationId { get; set; }

    /// <summary>Returned once, only when an anonymous caller creates a conversation. Store it client-side.</summary>
    public string? GuestToken { get; set; }

    public string Answer { get; set; } = string.Empty;
    public List<AiCitationDto> Citations { get; set; } = new();

    /// <summary>How the answer was produced: "retrieval", "rules", "escalation" or "insufficient".</summary>
    public string Grounding { get; set; } = string.Empty;

    /// <summary>"high", "medium" or "low" — evidence quality, not legal certainty.</summary>
    public string Confidence { get; set; } = string.Empty;

    public bool RequiresProfessionalAdvice { get; set; }
    public string Disclaimer { get; set; } = string.Empty;
    public List<string> SuggestedQuestions { get; set; } = new();
}

public class AiChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<AiCitationDto> Citations { get; set; } = new();
    public string? Grounding { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AiConversationSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AiConversationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<AiChatMessageDto> Messages { get; set; } = new();
}

public class ExplainReportRequestDto
{
    /// <summary>The calculator input; the deterministic engine recomputes the report server-side.</summary>
    public TaxCalculationRequestDto Calculation { get; set; } = new();
}

public class ExplainReportResponseDto
{
    public string Explanation { get; set; } = string.Empty;
    public List<AiCitationDto> Citations { get; set; } = new();
    public string Disclaimer { get; set; } = string.Empty;
}
