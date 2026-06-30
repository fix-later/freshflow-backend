namespace FreshFlow.Infrastructure.Persistence.Entities;

/// <summary>
/// Persisted row backing one AI assistant conversation session (T3). Owned and read exclusively by
/// <c>FreshFlow.API.Assistant.Conversation.DbConversationStore</c> — no module's Domain/Application
/// depends on this type, so it lives in the shared Persistence project rather than under a module.
/// <see cref="StateJson"/> holds the serialized <c>ConversationState</c> (turns, draft order id, etc.);
/// the assistant host owns that shape, this entity only owns the storage envelope (id, TTL, timestamps).
/// </summary>
public sealed class AssistantConversation
{
    private AssistantConversation() { } // EF Core

    public AssistantConversation(
        Guid id,
        string sessionId,
        Guid userId,
        Guid? marketId,
        string stateJson,
        DateTime createdAt,
        DateTime updatedAt,
        DateTime expiresAt)
    {
        Id = id;
        SessionId = sessionId;
        UserId = userId;
        MarketId = marketId;
        StateJson = stateJson;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public string SessionId { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public Guid? MarketId { get; private set; }
    public string StateJson { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    /// <summary>Overwrites the persisted state and slides the TTL forward — called on every save.</summary>
    public void Touch(string stateJson, DateTime now, TimeSpan ttl)
    {
        StateJson = stateJson;
        UpdatedAt = now;
        ExpiresAt = now + ttl;
    }
}
