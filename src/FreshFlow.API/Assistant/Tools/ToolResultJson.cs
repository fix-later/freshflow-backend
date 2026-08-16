using System.Text.Json;
using System.Text.Json.Nodes;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.API.Assistant.Tools;

/// <summary>
/// Maps a MediatR <see cref="Result{T}"/> to the JSON string appended to conversation history as a
/// tool result. Centralized here so every <see cref="ToolDefinitions"/> handler produces the same
/// error shape (<c>{ "error": Code, "message": Message }</c>) and the same sensitive-field stripping
/// — handlers must not hand-roll their own JSON mapping.
/// </summary>
internal static class ToolResultJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Maps success ⇒ serialized <paramref name="value"/>, failure ⇒ structured error JSON.</summary>
    public static string From<T>(Result<T> result, params string[] fieldsToStrip) =>
        result.IsSuccess
            ? Success(result.Value, fieldsToStrip)
            : Error(result.Error.Code, result.Error.Message);

    /// <summary>
    /// Serializes <paramref name="value"/> and removes <paramref name="fieldsToStrip"/> (PascalCase
    /// C# property names, e.g. <c>RemainingCreditAfter</c>) before the payload ever reaches the LLM
    /// prompt — see the data handling constraint in
    /// DESIGN-2026-06-22-ai-assistant-tier2-orchestration.md §2.
    /// </summary>
    public static string Success<T>(T value, params string[] fieldsToStrip)
    {
        if (fieldsToStrip.Length == 0)
        {
            return JsonSerializer.Serialize(value, SerializerOptions);
        }

        var node = JsonSerializer.SerializeToNode(value, SerializerOptions)?.AsObject();
        foreach (var field in fieldsToStrip)
        {
            // SerializerOptions camel-cases property names on output, so the C# PascalCase name
            // passed in (matching the DTO source) must be camel-cased to find the matching node key.
            node?.Remove(JsonNamingPolicy.CamelCase.ConvertName(field));
        }

        return node?.ToJsonString(SerializerOptions) ?? "null";
    }

    /// <summary>Structured error the LLM can read and react to — never an exception.</summary>
    public static string Error(string code, string message) =>
        JsonSerializer.Serialize(new ToolErrorPayload(code, message), SerializerOptions);

    private sealed record ToolErrorPayload(string Error, string Message);
}
