using System.Text.Json;
using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.API.Assistant.Conversation;
using FreshFlow.API.Assistant.Dtos;
using FreshFlow.API.Assistant.Safety;
using FreshFlow.API.Assistant.Tools;
using Microsoft.Extensions.Options;

namespace FreshFlow.API.Assistant;

/// <summary>
/// Drives one assistant chat turn (T5, non-streaming): repeatedly asks the LLM for the next step and
/// dispatches any tool call it requests, until the model returns a final text reply or the tool-hop
/// budget (<see cref="AssistantOptions.MaxToolHops"/>) is exhausted. The orchestrator is provider-agnostic
/// (depends only on <see cref="IAssistantChatClient"/>) and storage-agnostic (the caller owns load/save
/// of <see cref="ConversationState"/>), which keeps it unit-testable with a scripted fake chat client.
/// <para>
/// The two-phase confirmation gate (<see cref="ConfirmationGate"/>) is enforced here: a
/// <c>confirm_order</c> tool call without a matching client confirmation flag never reaches the tool
/// registry — instead the orchestrator runs a preview and returns a <see cref="PendingConfirmation"/>.
/// </para>
/// </summary>
public sealed class AssistantOrchestrator(
    IAssistantChatClient chatClient,
    IAssistantToolRegistry toolRegistry,
    ConfirmationGate confirmationGate,
    IOptions<AssistantOptions> options)
{
    private const string CreateDraftOrderToolName = "create_draft_order";
    private const string PreviewConfirmationToolName = "preview_confirmation";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly ConversationTurn SystemTurn =
        new(ConversationRole.System, AssistantSystemPrompt.Text);

    private readonly int _maxToolHops = options.Value.MaxToolHops;

    /// <summary>
    /// Runs the LLM ↔ tool loop for the conversation in <paramref name="state"/> (whose last turn is the
    /// user's new message). Returns the assistant's reply plus the updated state to persist.
    /// </summary>
    /// <param name="state">Conversation so far, user message already appended.</param>
    /// <param name="confirmOrderIdFlag">Explicit client confirmation flag for this request (T4), if any.</param>
    public async Task<AssistantTurnOutcome> RunAsync(
        ConversationState state,
        Guid? confirmOrderIdFlag,
        Guid? deliveryAddressIdFlag,
        CancellationToken ct = default)
    {
        var ctx = new AssistantToolInvocationContext(state.UserId, state.MarketId, deliveryAddressIdFlag);
        var tools = toolRegistry.Tools;
        var draftOrderId = state.CurrentDraftOrderId;

        for (var hop = 0; hop < _maxToolHops; hop++)
        {
            var llmState = state with { Turns = [SystemTurn, .. state.Turns] };
            var turn = await chatClient.CompleteAsync(llmState, tools, ct);

            if (!turn.IsToolCall)
            {
                state = AppendAssistantText(state, turn.Text ?? string.Empty);
                return new AssistantTurnOutcome(turn.Text ?? string.Empty, state, PendingConfirmation: null, draftOrderId);
            }

            var gate = confirmationGate.Evaluate(
                turn.ToolName!, turn.ToolArgsJson, confirmOrderIdFlag, deliveryAddressIdFlag);
            if (gate.IsBlocked)
            {
                return await BuildPendingConfirmationAsync(
                    state, gate.OrderId, gate.DeliveryAddressId, ctx, draftOrderId, ct);
            }

            var resultJson = await toolRegistry.InvokeAsync(turn.ToolName!, ParseArgs(turn.ToolArgsJson), ctx, ct);
            state = AppendToolExchange(state, turn, resultJson);

            if (turn.ToolName == CreateDraftOrderToolName && TryExtractOrderId(resultJson) is { } newDraftId)
            {
                draftOrderId = newDraftId;
                state = state with { CurrentDraftOrderId = newDraftId };
            }
        }

        // Tool-hop budget exhausted — bail out with a safe message rather than looping forever.
        const string safeMessage =
            "Xin lỗi, yêu cầu này cần nhiều bước hơn dự kiến nên tôi tạm dừng. Bạn vui lòng thử lại với yêu cầu cụ thể hơn nhé.";
        state = AppendAssistantText(state, safeMessage);
        return new AssistantTurnOutcome(safeMessage, state, PendingConfirmation: null, draftOrderId);
    }

    /// <summary>
    /// Confirmation was withheld by the gate: run a preview for the pending order (if known) and return
    /// a <see cref="PendingConfirmation"/> so the client can surface an explicit confirm button. The
    /// <c>confirm_order</c> tool is deliberately never dispatched on this path.
    /// </summary>
    private async Task<AssistantTurnOutcome> BuildPendingConfirmationAsync(
        ConversationState state,
        Guid? pendingOrderId,
        Guid? deliveryAddressId,
        AssistantToolInvocationContext ctx,
        Guid? draftOrderId,
        CancellationToken ct)
    {
        if (pendingOrderId is null)
        {
            const string clarify =
                "Tôi chưa rõ bạn muốn xác nhận đơn hàng nào. Bạn vui lòng cho tôi biết đơn hàng cụ thể nhé.";
            return new AssistantTurnOutcome(clarify, AppendAssistantText(state, clarify), PendingConfirmation: null, draftOrderId);
        }

        if (deliveryAddressId is null)
        {
            const string selectAddress =
                "Vui lòng chọn địa chỉ giao hàng trước khi xác nhận đơn.";
            return new AssistantTurnOutcome(
                selectAddress,
                AppendAssistantText(state, selectAddress),
                PendingConfirmation: null,
                draftOrderId);
        }

        var previewArgs = ToJsonElement(new { orderId = pendingOrderId.Value });
        var previewJson = await toolRegistry.InvokeAsync(PreviewConfirmationToolName, previewArgs, ctx, ct);

        const string reply =
            "Đơn hàng của bạn đã sẵn sàng. Vui lòng kiểm tra tóm tắt và bấm xác nhận để tôi đặt đơn giúp bạn.";
        var updatedState = AppendAssistantText(state, reply);
        var pending = new PendingConfirmation(pendingOrderId.Value, deliveryAddressId.Value, previewJson);
        return new AssistantTurnOutcome(reply, updatedState, pending, draftOrderId);
    }

    private static ConversationState AppendAssistantText(ConversationState state, string text) =>
        state with { Turns = [.. state.Turns, new ConversationTurn(ConversationRole.Assistant, text)] };

    private static ConversationState AppendToolExchange(ConversationState state, AssistantTurnResult turn, string resultJson) =>
        state with
        {
            Turns =
            [
                .. state.Turns,
                new ConversationTurn(
                    ConversationRole.Assistant,
                    Text: null,
                    ToolCallId: turn.ToolCallId,
                    ToolName: turn.ToolName,
                    ToolCallArgsJson: turn.ToolArgsJson),
                new ConversationTurn(
                    ConversationRole.Tool,
                    Text: null,
                    ToolCallId: turn.ToolCallId,
                    ToolResultJson: resultJson)
            ]
        };

    /// <summary>Parses the LLM's serialized argument string into a <see cref="JsonElement"/> for the registry.</summary>
    private static JsonElement ParseArgs(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
        {
            return ToJsonElement(new { });
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ToJsonElement(new { });
        }
    }

    private static JsonElement ToJsonElement(object value) =>
        JsonSerializer.SerializeToElement(value, SerializerOptions);

    /// <summary>
    /// Extracts the created order id from a successful <c>create_draft_order</c> tool result, or null
    /// when the result is an error envelope or carries no usable <c>orderId</c>.
    /// </summary>
    private static Guid? TryExtractOrderId(string resultJson)
    {
        try
        {
            using var document = JsonDocument.Parse(resultJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("error", out _))
            {
                return null;
            }

            return root.TryGetProperty("orderId", out var idElement) && idElement.TryGetGuid(out var orderId)
                ? orderId
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Outcome of one <see cref="AssistantOrchestrator.RunAsync"/> call: the reply to show the user, the
/// updated <see cref="ConversationState"/> to persist, and optional confirmation/draft signals.
/// </summary>
public sealed record AssistantTurnOutcome(
    string Reply,
    ConversationState State,
    PendingConfirmation? PendingConfirmation,
    Guid? DraftOrderId);
