namespace TaxNavi.Domain.Entities;

/// <summary>
/// A tax-assistant conversation. Owned either by a signed-in user (UserId) or by a guest
/// holding a bearer token (only the SHA-256 hash is stored, mirroring guest report access).
/// </summary>
public class ChatConversation
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>SHA-256 hex hash of the guest access token; null for signed-in owners.</summary>
    public string? GuestTokenHash { get; set; }

    /// <summary>Short label derived from the first question, for the history list.</summary>
    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
