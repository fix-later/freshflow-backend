using System.Text.Json;
using FreshFlow.API.Assistant.Tools;

namespace FreshFlow.API.Assistant.Abstractions;

/// <summary>
/// Catalog of LLM-callable tools (T2). The orchestrator (T5) reads <see cref="Tools"/> to build the
/// provider's tool declarations, then calls <see cref="InvokeAsync"/> when the LLM requests a tool
/// call — it never touches an individual <see cref="AssistantTool.Handler"/> directly.
/// </summary>
public interface IAssistantToolRegistry
{
    /// <summary>All registered tools, exposed to <see cref="IAssistantChatClient.CompleteAsync"/>.</summary>
    public IReadOnlyList<AssistantTool> Tools { get; }

    /// <summary>
    /// Dispatches one LLM tool call: looks up <paramref name="toolName"/>, hands
    /// <paramref name="argsJson"/> and <paramref name="ctx"/> to its handler, and returns the JSON
    /// string to append to conversation history as the tool result. Never throws for an unknown
    /// tool name or malformed args — both come back as a structured JSON error the LLM can read and
    /// self-correct from (see <see cref="AssistantTool"/> remarks).
    /// </summary>
    public Task<string> InvokeAsync(
        string toolName,
        JsonElement argsJson,
        AssistantToolInvocationContext ctx,
        CancellationToken ct = default);
}
