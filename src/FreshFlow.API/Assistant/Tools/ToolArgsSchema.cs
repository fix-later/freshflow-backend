using System.Text.Json;

namespace FreshFlow.API.Assistant.Tools;

/// <summary>
/// Tiny JSON-schema builder for <see cref="AssistantTool.ParametersSchema"/>. The 5 MVP tools only
/// need flat objects with string/number/boolean/array properties — a full JSON-schema library would
/// be overkill (YAGNI), so this just assembles the minimal subset the OpenAI-compatible tool-calling
/// contract expects.
/// </summary>
internal static class ToolArgsSchema
{
    public static JsonElement Object(object properties, string[] required) =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties,
            required
        });
}
