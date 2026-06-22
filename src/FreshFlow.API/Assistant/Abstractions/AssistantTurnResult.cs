namespace FreshFlow.API.Assistant.Abstractions;

/// <summary>
/// Outcome of one <see cref="IAssistantChatClient.CompleteAsync"/> call: the LLM either produced
/// a final text reply, or asked to invoke exactly one tool. Never both — callers should branch on
/// <see cref="IsToolCall"/>.
/// </summary>
public sealed record AssistantTurnResult
{
    /// <summary>Final natural-language reply. Set when the LLM did not request a tool call.</summary>
    public string? Text { get; private init; }

    /// <summary>Tool the LLM wants to invoke (e.g. "search_products"). Null when <see cref="Text"/> is set.</summary>
    public string? ToolName { get; private init; }

    /// <summary>Tool call id echoed back by the provider — required to attach the tool result message.</summary>
    public string? ToolCallId { get; private init; }

    /// <summary>Raw JSON object of arguments the LLM supplied for <see cref="ToolName"/>.</summary>
    public string? ToolArgsJson { get; private init; }

    public bool IsToolCall => ToolName is not null;

    public static AssistantTurnResult FromText(string text) => new() { Text = text };

    public static AssistantTurnResult FromToolCall(string toolCallId, string toolName, string toolArgsJson) =>
        new() { ToolCallId = toolCallId, ToolName = toolName, ToolArgsJson = toolArgsJson };
}
