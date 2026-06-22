namespace FreshFlow.API.Assistant.Conversation;

/// <summary>
/// In-memory view of one assistant conversation, passed to
/// <see cref="FreshFlow.API.Assistant.Llm.IAssistantChatClient"/> and round-tripped through
/// <c>IConversationStore</c> (T3 — DB-backed persistence, TTL, lazy-expire).
/// </summary>
/// <param name="SessionId">Client-generated session handle.</param>
/// <param name="UserId">Authenticated user, injected server-side from the JWT — never from the LLM.</param>
/// <param name="MarketId">Active market context, injected server-side from the request — never from the LLM.</param>
/// <param name="Turns">Conversation history (the system prompt is added by the orchestrator, not stored here).</param>
/// <param name="CurrentDraftOrderId">
/// The draft order currently being built/discussed in this session, if any. Carried across turns so
/// the orchestrator and <c>ConfirmationGate</c> (T4) can resolve "this order" without the LLM having
/// to re-supply an order id it was never given in the first place.
/// </param>
public sealed record ConversationState(
    string SessionId,
    Guid UserId,
    Guid? MarketId,
    IReadOnlyList<ConversationTurn> Turns,
    Guid? CurrentDraftOrderId = null);
