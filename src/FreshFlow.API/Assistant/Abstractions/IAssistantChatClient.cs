using FreshFlow.API.Assistant.Conversation;
using FreshFlow.API.Assistant.Tools;

namespace FreshFlow.API.Assistant.Abstractions;

/// <summary>
/// Provider-agnostic abstraction over the LLM used by the assistant orchestrator (T5). The only
/// implementation today is <see cref="FreshFlow.API.Assistant.Llm.ZenMuxChatClient"/> (GLM 5.2 Free
/// via ZenMux), but the orchestrator must never depend on provider-specific types — swapping to a
/// paid model later is a single new class behind this interface.
/// </summary>
public interface IAssistantChatClient
{
    /// <summary>
    /// Sends the conversation history + available tool declarations to the LLM and returns either
    /// a final text reply or a single tool-call request. Implementations own retry/timeout handling
    /// for transient provider failures (5xx/timeout/429) — callers see only success or a thrown
    /// exception for non-recoverable failures.
    /// </summary>
    public Task<AssistantTurnResult> CompleteAsync(
        ConversationState state,
        IReadOnlyList<AssistantTool> tools,
        CancellationToken ct = default);
}
