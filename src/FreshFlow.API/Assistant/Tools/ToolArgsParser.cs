using System.Text.Json;

namespace FreshFlow.API.Assistant.Tools;

/// <summary>
/// Deserializes the LLM-supplied <see cref="JsonElement"/> args into a typed record for one tool
/// handler. Malformed JSON or a type mismatch never throws past this point — it surfaces as the same
/// structured tool error every other failure uses, so the LLM can read it and retry with corrected
/// arguments instead of the orchestrator crashing.
/// </summary>
internal static class ToolArgsParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Tries to deserialize <paramref name="argsJson"/> into <typeparamref name="TArgs"/>. Returns
    /// <c>false</c> with <paramref name="error"/> set to a structured "bad schema" tool result on
    /// failure — callers should return that string directly rather than throwing.
    /// </summary>
    public static bool TryParse<TArgs>(JsonElement argsJson, out TArgs? args, out string? error)
    {
        try
        {
            args = argsJson.Deserialize<TArgs>(Options);
            if (args is null)
            {
                error = ToolResultJson.Error("INVALID_TOOL_ARGS", "Tool arguments must be a JSON object.");
                return false;
            }

            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            args = default;
            error = ToolResultJson.Error("INVALID_TOOL_ARGS", $"Could not parse tool arguments: {ex.Message}");
            return false;
        }
    }
}
