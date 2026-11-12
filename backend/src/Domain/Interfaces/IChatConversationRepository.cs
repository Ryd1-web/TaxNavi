using TaxNavi.Domain.Entities;

namespace TaxNavi.Domain.Interfaces;

public interface IChatConversationRepository
{
    Task<ChatConversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Conversation with its messages in ordinal order.</summary>
    Task<ChatConversation?> GetWithMessagesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<ChatConversation>> ListForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default);
    Task AddAsync(ChatConversation conversation, CancellationToken cancellationToken = default);
    Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);
    Task UpdateAsync(ChatConversation conversation, CancellationToken cancellationToken = default);
    Task<int> NextOrdinalAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
