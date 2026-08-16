namespace FreshFlow.API.Assistant.Conversation;

/// <summary>Role of a single message in an assistant conversation.</summary>
public enum ConversationRole
{
    /// <summary>
    /// The orchestrator's system prompt. Injected transiently at LLM-call time (T5) and never
    /// persisted in <c>ConversationState.Turns</c> — see the remarks on <c>ConversationState.Turns</c>.
    /// </summary>
    System,
    User,
    Assistant,
    Tool
}

/// <summary>
/// One message in a conversation's history. <paramref name="ToolName"/>/<paramref name="ToolCallArgsJson"/>
/// are populated when <see cref="Role"/> is <see cref="ConversationRole.Assistant"/> and the turn was a
/// tool call; <paramref name="ToolResultJson"/> is populated for the matching <see cref="ConversationRole.Tool"/>
/// reply appended after dispatch (see T2 AssistantToolRegistry).
/// </summary>
public sealed record ConversationTurn(
    ConversationRole Role,
    string? Text,
    string? ToolCallId = null,
    string? ToolName = null,
    string? ToolCallArgsJson = null,
    string? ToolResultJson = null);
