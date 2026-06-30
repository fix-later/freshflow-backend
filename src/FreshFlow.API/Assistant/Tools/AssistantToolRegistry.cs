using System.Text.Json;
using FreshFlow.API.Assistant.Abstractions;

namespace FreshFlow.API.Assistant.Tools;

/// <summary>
/// <see cref="IAssistantToolRegistry"/> implementation. Holds the fixed set of tools declared by
/// <see cref="ToolDefinitions"/> and dispatches by name. The registry itself never talks to MediatR
/// or maps DTOs — each <see cref="AssistantTool.Handler"/> owns that; the registry's only
/// responsibility is the lookup and turning "tool not found" into the same structured-error shape
/// every handler uses for business failures, so the LLM always sees one error contract.
/// </summary>
public sealed class AssistantToolRegistry : IAssistantToolRegistry
{
    public IReadOnlyList<AssistantTool> Tools { get; }

    public AssistantToolRegistry(IEnumerable<AssistantTool> tools)
    {
        Tools = tools.ToList();
    }

    public async Task<string> InvokeAsync(
        string toolName,
        JsonElement argsJson,
        AssistantToolInvocationContext ctx,
        CancellationToken ct = default)
    {
        var tool = Tools.FirstOrDefault(t => t.Name == toolName);

        if (tool?.Handler is null)
        {
            return ToolResultJson.Error("UNKNOWN_TOOL", $"Tool '{toolName}' does not exist.");
        }

        return await tool.Handler(argsJson, ctx, ct);
    }
}
