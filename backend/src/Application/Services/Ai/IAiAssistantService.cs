using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Services;

public enum AiOutcome
{
    Ok,
    InvalidRequest,
    NotFound,
    QuotaExceeded
}

public record AiResult<T>(AiOutcome Outcome, T? Data = default, string? Message = null)
{
    public static AiResult<T> Ok(T data) => new(AiOutcome.Ok, data);
    public static AiResult<T> Fail(AiOutcome outcome, string message) => new(outcome, default, message);
}

/// <summary>The RAG-powered conversational tax assistant.</summary>
public interface IAiAssistantService
{
    /// <summary>
    /// Answer one question, creating or continuing a conversation. <paramref name="clientKey"/> is a
    /// non-reversible key for anonymous callers (used for quotas), null for signed-in users.
    /// </summary>
    Task<AiResult<AiChatResponseDto>> ChatAsync(Guid? userId, string? clientKey, AiChatRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Run the full answer pipeline for one question without a conversation, persistence or quota.
    /// Used by the admin evaluation harness only.
    /// </summary>
    Task<AiChatResponseDto> AnswerOnceAsync(string question, CancellationToken cancellationToken = default);

    Task<AiResult<AiConversationDto>> GetConversationAsync(Guid? userId, string? guestToken, Guid conversationId, CancellationToken cancellationToken = default);

    Task<List<AiConversationSummaryDto>> ListConversationsAsync(Guid userId, CancellationToken cancellationToken = default);
}
