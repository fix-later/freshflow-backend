using FreshFlow.API.Assistant.Conversation;

namespace FreshFlow.API.Assistant.Abstractions;

/// <summary>
/// Persists conversation history across chat turns (T3). Backed by Postgres in v1
/// (<c>DbConversationStore</c>); a future <c>RedisConversationStore</c> is a drop-in replacement —
/// the orchestrator (T5) only ever depends on this interface.
/// </summary>
public interface IConversationStore
{
    /// <summary>
    /// Loads the conversation for <paramref name="sessionId"/>, or <c>null</c> if it doesn't exist or
    /// has expired (lazy-expire — an expired row reads back as if it were never there).
    /// </summary>
    public Task<ConversationState?> LoadAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Creates or overwrites the conversation for <paramref name="state"/>'s session id and slides its
    /// TTL forward. History longer than the store's retention window is trimmed before this call.
    /// Returns <c>false</c> when the session id belongs to another user.
    /// </summary>
    public Task<bool> SaveAsync(ConversationState state, CancellationToken ct = default);
}
