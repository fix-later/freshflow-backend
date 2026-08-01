using System.Text.Json;

namespace FreshFlow.API.Assistant.Tools;

/// <summary>
/// Declares one LLM-callable tool and owns its dispatch handler. Each tool is a thin 1:1 wrapper
/// over an existing MediatR command/query (see DESIGN-2026-06-22-ai-assistant-tier2-orchestration.md
/// §3) — the handler maps JSON args to a record, calls <c>ISender.Send</c>, and maps the
/// <c>Result&lt;T&gt;</c> back to JSON. Tool definitions (T2 <c>ToolDefinitions.cs</c>) populate
/// <see cref="Handler"/>; <see cref="FreshFlow.API.Assistant.Llm.IAssistantChatClient"/> only ever
/// reads <see cref="Name"/>/<see cref="Description"/>/<see cref="ParametersSchema"/> to build the
/// provider's tool declaration — it never invokes <see cref="Handler"/> directly.
/// </summary>
/// <param name="Name">Tool name as exposed to the LLM (e.g. "search_products").</param>
/// <param name="Description">Human-readable description the LLM uses to decide when to call it.</param>
/// <param name="ParametersSchema">JSON schema (object) describing the tool's input arguments.</param>
/// <param name="Handler">
/// Dispatch delegate: receives the LLM-supplied JSON args plus the server-injected invocation
/// context (UserId/MarketId/DeliveryAddressId — never supplied by the LLM) and returns the JSON result to append to
/// conversation history. Populated by <c>AssistantToolRegistry</c> (T2); left <c>null</c> here only
/// so this record compiles standalone in T1 unit tests.
/// </param>
public sealed record AssistantTool(
    string Name,
    string Description,
    JsonElement ParametersSchema,
    Func<JsonElement, AssistantToolInvocationContext, CancellationToken, Task<string>>? Handler = null);

/// <summary>
/// Server-injected context passed to an <see cref="AssistantTool"/> handler. <see cref="UserId"/>
/// <see cref="UserId"/>, <see cref="MarketId"/>, and <see cref="DeliveryAddressId"/> come from the
/// authenticated request/session, never from
/// LLM-supplied tool arguments — see the data-handling constraint in §2 of the design doc.
/// </summary>
/// <param name="UserId">Authenticated user, injected server-side.</param>
/// <param name="MarketId">Active market context, injected server-side.</param>
/// <param name="DeliveryAddressId">Delivery address explicitly selected by the client.</param>
public sealed record AssistantToolInvocationContext(
    Guid UserId,
    Guid? MarketId,
    Guid? DeliveryAddressId = null);
