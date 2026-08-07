using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.API.Assistant.Conversation;
using FreshFlow.API.Assistant.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using Polly.Timeout;

namespace FreshFlow.API.Assistant.Llm;

/// <summary>
/// <see cref="IAssistantChatClient"/> implementation backed by Gemini on Vertex AI (Gemini
/// Enterprise Agent Platform). Vertex exposes an OpenAI-compatible Chat Completions surface, so this
/// wraps the official OpenAI SDK's <see cref="OpenAI.Chat.ChatClient"/> (via Microsoft.Extensions.AI's
/// <c>AsIChatClient()</c>) pointed at the Vertex endpoint instead of api.openai.com. Auth is not a
/// static key — <see cref="GcpAuthHandler"/> on the underlying HttpClient injects a rotating GCP
/// OAuth2 bearer token, so the SDK credential here is a throwaway placeholder. The orchestrator only
/// ever sees <see cref="IAssistantChatClient"/> — no OpenAI/Vertex type leaks past this class.
/// </summary>
public sealed class GeminiChatClient : IAssistantChatClient
{
    // The OpenAI SDK requires a non-empty credential, but GcpAuthHandler overwrites the
    // Authorization header before every request — this value is never sent.
    private const string PlaceholderCredential = "vertex-auth-via-adc";

    private readonly IChatClient _inner;
    private readonly GeminiOptions _options;

    /// <summary>Production constructor — builds the OpenAI-compatible client pointed at Vertex AI.</summary>
    public GeminiChatClient(IOptions<GeminiOptions> options, HttpClient httpClient)
        : this(options.Value, BuildInnerClient(options.Value, httpClient))
    {
    }

    /// <summary>Test-only seam — lets unit tests verify request/response mapping against a mocked <see cref="IChatClient"/>.</summary>
    internal GeminiChatClient(GeminiOptions options, IChatClient inner)
    {
        _options = options;
        _inner = inner;
    }

    private static IChatClient BuildInnerClient(GeminiOptions options, HttpClient httpClient)
    {
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = options.BuildEndpoint(),
            // Retry/backoff for 5xx/timeout/429 is handled by the named HttpClient's standard
            // resilience handler (see DependencyInjection.cs) — disable the SDK's own retry so
            // failures aren't retried twice.
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            Transport = new HttpClientPipelineTransport(httpClient)
        };

        return new OpenAIClient(new ApiKeyCredential(PlaceholderCredential), clientOptions)
            .GetChatClient(options.Model)
            .AsIChatClient();
    }

    public async Task<AssistantTurnResult> CompleteAsync(
        ConversationState state,
        IReadOnlyList<AssistantTool> tools,
        CancellationToken ct = default)
    {
        var messages = state.Turns.Select(ToChatMessage).ToList();

        var chatOptions = new ChatOptions
        {
            ModelId = _options.Model,
            Tools = tools.Count == 0 ? null : tools.Select(ToAiTool).Cast<AITool>().ToList()
        };

        ChatResponse response;
        try
        {
            response = await _inner.GetResponseAsync(messages, chatOptions, ct);
        }
        catch (ClientResultException ex)
        {
            var failure = ex.Status switch
            {
                401 or 403 => AssistantProviderFailure.AuthenticationFailed,
                429 => AssistantProviderFailure.RateLimited,
                _ => AssistantProviderFailure.Unavailable
            };
            throw new AssistantProviderException(failure, ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new AssistantProviderException(AssistantProviderFailure.Timeout, ex);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new AssistantProviderException(AssistantProviderFailure.Timeout, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AssistantProviderException(AssistantProviderFailure.Unavailable, ex);
        }

        var functionCall = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .FirstOrDefault();

        if (functionCall is not null)
        {
            return AssistantTurnResult.FromToolCall(
                functionCall.CallId,
                functionCall.Name,
                JsonSerializer.Serialize(functionCall.Arguments));
        }

        return AssistantTurnResult.FromText(response.Text);
    }

    private static ChatMessage ToChatMessage(ConversationTurn turn) => turn.Role switch
    {
        ConversationRole.System => new ChatMessage(ChatRole.System, turn.Text ?? string.Empty),
        ConversationRole.User => new ChatMessage(ChatRole.User, turn.Text ?? string.Empty),
        ConversationRole.Tool => new ChatMessage(
            ChatRole.Tool,
            [new FunctionResultContent(turn.ToolCallId ?? string.Empty, turn.ToolResultJson)]),
        ConversationRole.Assistant when turn.ToolName is not null => new ChatMessage(
            ChatRole.Assistant,
            [new FunctionCallContent(turn.ToolCallId ?? string.Empty, turn.ToolName, ParseArgs(turn.ToolCallArgsJson))]),
        _ => new ChatMessage(ChatRole.Assistant, turn.Text ?? string.Empty)
    };

    private static IDictionary<string, object?>? ParseArgs(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(json);

    private static AIFunctionDeclaration ToAiTool(AssistantTool tool) =>
        AIFunctionFactory.CreateDeclaration(tool.Name, tool.Description, tool.ParametersSchema);
}
