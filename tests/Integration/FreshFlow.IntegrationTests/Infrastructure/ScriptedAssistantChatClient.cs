using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.API.Assistant.Conversation;
using FreshFlow.API.Assistant.Tools;

namespace FreshFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Test double for <see cref="IAssistantChatClient"/> used by the assistant integration tests so they
/// never call the real GLM/ZenMux endpoint (SCRUM-253 / DoD: "không gọi GLM thật trong CI"). A test
/// scripts the LLM's turns with <see cref="Enqueue"/>; each chat request dequeues the next turn. When
/// the script is empty the client returns a harmless final text reply, so rate-limit / smoke requests
/// that don't care about the LLM output still succeed.
/// </summary>
public sealed class ScriptedAssistantChatClient : IAssistantChatClient
{
    private readonly object _lock = new();
    private readonly Queue<AssistantTurnResult> _script = new();

    /// <summary>Queues the LLM turns to be returned, in order, by subsequent <see cref="CompleteAsync"/> calls.</summary>
    public void Enqueue(params AssistantTurnResult[] turns)
    {
        lock (_lock)
        {
            foreach (var turn in turns)
            {
                _script.Enqueue(turn);
            }
        }
    }

    /// <summary>Clears any remaining scripted turns so tests sharing a factory don't bleed into each other.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _script.Clear();
        }
    }

    public Task<AssistantTurnResult> CompleteAsync(
        ConversationState state, IReadOnlyList<AssistantTool> tools, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var turn = _script.Count > 0
                ? _script.Dequeue()
                : AssistantTurnResult.FromText("Tôi có thể giúp gì thêm cho bạn?");
            return Task.FromResult(turn);
        }
    }
}
