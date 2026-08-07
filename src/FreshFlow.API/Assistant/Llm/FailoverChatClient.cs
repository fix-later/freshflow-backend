using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.API.Assistant.Conversation;
using FreshFlow.API.Assistant.Tools;
using Microsoft.Extensions.Logging;

namespace FreshFlow.API.Assistant.Llm;

/// <summary>
/// <see cref="IAssistantChatClient"/> that tries a configured, priority-ordered list of provider
/// clients and falls back to the next provider when a higher-priority one fails recoverably
/// (quota/rate-limit, timeout, or 5xx — every <see cref="AssistantProviderException"/> except
/// <see cref="AssistantProviderFailure.AuthenticationFailed"/>). Authentication failures are
/// rethrown immediately so a misconfigured key surfaces instead of being silently masked by a
/// fallback. If every provider fails, the last provider's exception propagates.
/// </summary>
public sealed class FailoverChatClient : IAssistantChatClient
{
    private readonly IReadOnlyList<(string Name, IAssistantChatClient Client)> _providers;
    private readonly ILogger<FailoverChatClient> _logger;

    public FailoverChatClient(
        IReadOnlyList<(string Name, IAssistantChatClient Client)> providers,
        ILogger<FailoverChatClient> logger)
    {
        if (providers.Count == 0)
        {
            throw new ArgumentException("At least one assistant provider must be configured.", nameof(providers));
        }

        _providers = providers;
        _logger = logger;
    }

    public async Task<AssistantTurnResult> CompleteAsync(
        ConversationState state,
        IReadOnlyList<AssistantTool> tools,
        CancellationToken ct = default)
    {
        for (var i = 0; i < _providers.Count; i++)
        {
            var (name, client) = _providers[i];
            try
            {
                return await client.CompleteAsync(state, tools, ct);
            }
            // Fall back to the next provider only when this one failed recoverably AND a fallback
            // exists. The filter is false on the last provider, so its exception propagates; it is
            // also false for AuthenticationFailed, which propagates immediately from any position.
            catch (AssistantProviderException ex)
                when (ex.Failure != AssistantProviderFailure.AuthenticationFailed && i < _providers.Count - 1)
            {
                _logger.LogWarning(ex,
                    "Assistant provider {Provider} failed ({Failure}); falling back to next provider.",
                    name, ex.Failure);
            }
        }

        // Unreachable: the loop returns on success or the final iteration rethrows. Present so the
        // compiler sees a terminating path.
        throw new InvalidOperationException("No assistant provider produced a result.");
    }
}
