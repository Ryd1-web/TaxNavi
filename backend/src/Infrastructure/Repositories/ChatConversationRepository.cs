using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Infrastructure.Data;

namespace TaxNavi.Infrastructure.Repositories;

public class ChatConversationRepository : BaseRepository<ChatConversation>, IChatConversationRepository
{
    public ChatConversationRepository(TaxNaviDbContext context) : base(context) { }

    public Task<ChatConversation?> GetWithMessagesAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.ChatConversations
            .Include(c => c.Messages.OrderBy(m => m.Ordinal))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<List<ChatConversation>> ListForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default) =>
        _context.ChatConversations
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> NextOrdinalAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var current = await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .MaxAsync(m => (int?)m.Ordinal, cancellationToken);
        return (current ?? 0) + 1;
    }
}
