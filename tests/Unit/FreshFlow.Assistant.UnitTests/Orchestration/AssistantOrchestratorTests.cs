using System.Text.Json;
using FluentAssertions;
using FreshFlow.API.Assistant;
using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.API.Assistant.Conversation;
using FreshFlow.API.Assistant.Llm;
using FreshFlow.API.Assistant.Safety;
using FreshFlow.API.Assistant.Tools;
using Microsoft.Extensions.Options;

namespace FreshFlow.Assistant.UnitTests.Orchestration;

[Trait("Category", "Unit")]
public sealed class AssistantOrchestratorTests
{
    private static ConversationState NewState(string message = "xin chào") =>
        new(
            SessionId: "session-1",
            UserId: Guid.NewGuid(),
            MarketId: Guid.NewGuid(),
            Turns: [new ConversationTurn(ConversationRole.User, message)]);

    private static AssistantOrchestrator BuildOrchestrator(
        ScriptedChatClient chatClient,
        SpyToolRegistry registry,
        int maxToolHops = 6) =>
        new(
            chatClient,
            registry,
            new ConfirmationGate(),
            Options.Create(new ZenMuxOptions { ApiKey = "test", MaxToolHops = maxToolHops }));

    [Fact]
    public async Task RunAsync_returns_text_reply_without_dispatching_tools_when_llm_answers_directly()
    {
        // Arrange
        var chatClient = new ScriptedChatClient(AssistantTurnResult.FromText("Chào bạn, tôi giúp gì được?"));
        var registry = new SpyToolRegistry();
        var orchestrator = BuildOrchestrator(chatClient, registry);

        // Act
        var outcome = await orchestrator.RunAsync(NewState(), confirmOrderIdFlag: null);

        // Assert
        outcome.Reply.Should().Be("Chào bạn, tôi giúp gì được?");
        outcome.PendingConfirmation.Should().BeNull();
        registry.Invocations.Should().BeEmpty();
        outcome.State.Turns[^1].Role.Should().Be(ConversationRole.Assistant);
    }

    [Fact]
    public async Task RunAsync_loops_through_tool_calls_then_returns_the_final_reply_and_captures_draft_order_id()
    {
        // Arrange — search → create_draft → final text.
        var draftOrderId = Guid.NewGuid();
        var chatClient = new ScriptedChatClient(
            AssistantTurnResult.FromToolCall("c1", "search_products", "{\"searchText\":\"cà chua\"}"),
            AssistantTurnResult.FromToolCall("c2", "create_draft_order", "{\"items\":[]}"),
            AssistantTurnResult.FromText("Tôi đã tạo đơn nháp cho bạn."));
        var registry = new SpyToolRegistry()
            .Returning("search_products", "{\"items\":[]}")
            .Returning("create_draft_order", JsonSerializer.Serialize(new { orderId = draftOrderId }));
        var orchestrator = BuildOrchestrator(chatClient, registry);

        // Act
        var outcome = await orchestrator.RunAsync(NewState(), confirmOrderIdFlag: null);

        // Assert
        outcome.Reply.Should().Be("Tôi đã tạo đơn nháp cho bạn.");
        registry.Invocations.Select(i => i.ToolName).Should().Equal("search_products", "create_draft_order");
        outcome.DraftOrderId.Should().Be(draftOrderId);
        outcome.State.CurrentDraftOrderId.Should().Be(draftOrderId);
    }

    [Fact]
    public async Task RunAsync_blocks_confirm_order_without_flag_and_returns_pending_confirmation()
    {
        // Arrange — LLM asks to confirm, but no client confirmation flag is present.
        var orderId = Guid.NewGuid();
        var chatClient = new ScriptedChatClient(
            AssistantTurnResult.FromToolCall("c1", "confirm_order", JsonSerializer.Serialize(new { orderId })));
        var registry = new SpyToolRegistry()
            .Returning("preview_confirmation", "{\"totalAmount\":120000}");
        var orchestrator = BuildOrchestrator(chatClient, registry);

        // Act
        var outcome = await orchestrator.RunAsync(NewState(), confirmOrderIdFlag: null);

        // Assert — confirm_order NEVER dispatched; preview ran; pendingConfirmation surfaced.
        registry.Invocations.Select(i => i.ToolName).Should().NotContain("confirm_order");
        registry.Invocations.Select(i => i.ToolName).Should().Contain("preview_confirmation");
        outcome.PendingConfirmation.Should().NotBeNull();
        outcome.PendingConfirmation!.OrderId.Should().Be(orderId);
        outcome.PendingConfirmation.PreviewJson.Should().Contain("totalAmount");
    }

    [Fact]
    public async Task RunAsync_dispatches_confirm_order_when_the_client_flag_matches()
    {
        // Arrange — user pressed confirm for this exact order, then the LLM wraps up.
        var orderId = Guid.NewGuid();
        var chatClient = new ScriptedChatClient(
            AssistantTurnResult.FromToolCall("c1", "confirm_order", JsonSerializer.Serialize(new { orderId })),
            AssistantTurnResult.FromText("Đơn của bạn đã được xác nhận."));
        var registry = new SpyToolRegistry()
            .Returning("confirm_order", "{\"status\":\"confirmed\"}");
        var orchestrator = BuildOrchestrator(chatClient, registry);

        // Act
        var outcome = await orchestrator.RunAsync(NewState(), confirmOrderIdFlag: orderId);

        // Assert
        registry.Invocations.Select(i => i.ToolName).Should().Contain("confirm_order");
        outcome.PendingConfirmation.Should().BeNull();
        outcome.Reply.Should().Be("Đơn của bạn đã được xác nhận.");
    }

    [Fact]
    public async Task RunAsync_stops_with_a_safe_message_when_the_tool_hop_budget_is_exhausted()
    {
        // Arrange — the LLM never stops requesting tools.
        var loopingCalls = Enumerable.Range(0, 10)
            .Select(i => AssistantTurnResult.FromToolCall($"c{i}", "search_products", "{\"searchText\":\"x\"}"))
            .ToArray();
        var chatClient = new ScriptedChatClient(loopingCalls);
        var registry = new SpyToolRegistry().Returning("search_products", "{\"items\":[]}");
        var orchestrator = BuildOrchestrator(chatClient, registry, maxToolHops: 3);

        // Act
        var outcome = await orchestrator.RunAsync(NewState(), confirmOrderIdFlag: null);

        // Assert — bailed out after exactly the budget, with a safe non-technical message.
        registry.Invocations.Should().HaveCount(3);
        outcome.Reply.Should().Contain("nhiều bước hơn dự kiến");
        outcome.PendingConfirmation.Should().BeNull();
    }

    // ── Test doubles ────────────────────────────────────────────────────────────

    private sealed class ScriptedChatClient(params AssistantTurnResult[] script) : IAssistantChatClient
    {
        private readonly Queue<AssistantTurnResult> _script = new(script);

        public Task<AssistantTurnResult> CompleteAsync(
            ConversationState state, IReadOnlyList<AssistantTool> tools, CancellationToken ct = default) =>
            Task.FromResult(_script.Dequeue());
    }

    private sealed class SpyToolRegistry : IAssistantToolRegistry
    {
        private readonly Dictionary<string, string> _responses = new();

        public List<(string ToolName, JsonElement Args)> Invocations { get; } = [];

        public IReadOnlyList<AssistantTool> Tools { get; } = [];

        public SpyToolRegistry Returning(string toolName, string responseJson)
        {
            _responses[toolName] = responseJson;
            return this;
        }

        public Task<string> InvokeAsync(
            string toolName, JsonElement argsJson, AssistantToolInvocationContext ctx, CancellationToken ct = default)
        {
            Invocations.Add((toolName, argsJson.Clone()));
            return Task.FromResult(_responses.TryGetValue(toolName, out var json) ? json : "{}");
        }
    }
}
