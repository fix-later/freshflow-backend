using System.Text.Json;

namespace FreshFlow.API.Assistant.Safety;

/// <summary>
/// Two-phase confirmation safety gate (T4). Sits in front of the <c>confirm_order</c> tool inside the
/// orchestrator (T5): an order is only ever confirmed when the <em>client</em> sends back an explicit
/// <c>confirmOrderId</c> flag and a client-selected delivery address — set by the user pressing a
/// confirm button on a preview the assistant already showed — that match the order being confirmed.
/// The LLM can never satisfy
/// this on its own: it does not control the request body, so a model that "decides" to confirm without
/// the user's button press is blocked here before <c>ConfirmOrderCommand</c> is ever dispatched.
/// <para>
/// The gate is a pure decision function — it touches no MediatR, no DB, no HTTP — so all three branches
/// (missing flag / mismatched flag / matching flag) are exhaustively unit-testable.
/// </para>
/// <para>
/// Scope note (M1): the gate proves the client explicitly confirmed the order id the LLM named and
/// supplied the delivery address injected into the confirm command.
/// It does not cross-check that id against the session's <c>CurrentDraftOrderId</c> — the guarantee that
/// the order actually belongs to this session's user comes from the Tier-1 handlers
/// (<c>ConfirmOrderCommandHandler</c>/<c>PreviewOrderConfirmationQueryHandler</c>), which independently
/// bind UserId → Restaurant → Order. Keep that invariant if these layers are ever split.
/// </para>
/// </summary>
public sealed class ConfirmationGate
{
    /// <summary>Name of the single tool this gate guards (see <c>ToolDefinitions.ConfirmOrder</c>).</summary>
    public const string ConfirmOrderToolName = "confirm_order";

    /// <summary>
    /// Decides whether an LLM tool call may proceed. For any tool other than <c>confirm_order</c> the
    /// gate is <see cref="ConfirmationDecision.NotApplicable"/> and the orchestrator dispatches normally.
    /// For <c>confirm_order</c> it is <see cref="ConfirmationDecision.Allowed"/> only when
    /// <paramref name="confirmOrderIdFlag"/> is present and equals the <c>orderId</c> in
    /// <paramref name="toolArgsJson"/>, and <paramref name="deliveryAddressIdFlag"/> is present;
    /// otherwise it is <see cref="ConfirmationDecision.Blocked"/> and the
    /// orchestrator must run a preview / ask the user instead of confirming.
    /// </summary>
    /// <param name="toolName">Tool the LLM asked to call.</param>
    /// <param name="toolArgsJson">Raw JSON arguments the LLM supplied for the call (may be null/invalid).</param>
    /// <param name="confirmOrderIdFlag">
    /// Explicit confirmation flag from the client request body — non-null only when the user pressed the
    /// confirm button for a specific order. Never populated from the LLM.
    /// </param>
    /// <param name="deliveryAddressIdFlag">Address explicitly selected by the client.</param>
    public ConfirmationGateResult Evaluate(
        string toolName,
        string? toolArgsJson,
        Guid? confirmOrderIdFlag,
        Guid? deliveryAddressIdFlag)
    {
        if (!string.Equals(toolName, ConfirmOrderToolName, StringComparison.Ordinal))
        {
            return ConfirmationGateResult.NotApplicable;
        }

        var orderId = TryExtractOrderId(toolArgsJson);

        // The only path to a real confirmation: a client-supplied flag that matches the exact order the
        // LLM named. A missing flag, a mismatched flag, or args the LLM mangled all fall through to Block.
        if (confirmOrderIdFlag is not null
            && orderId is not null
            && confirmOrderIdFlag.Value == orderId.Value
            && deliveryAddressIdFlag is not null)
        {
            return ConfirmationGateResult.Allow(orderId.Value, deliveryAddressIdFlag.Value);
        }

        return ConfirmationGateResult.Block(orderId, deliveryAddressIdFlag);
    }

    /// <summary>
    /// Pulls the <c>orderId</c> Guid out of the LLM's tool arguments, returning null when the args are
    /// absent, unparseable, or carry a missing/malformed <c>orderId</c> — every such case must Block, so
    /// parsing failures are deliberately swallowed into a null rather than thrown.
    /// </summary>
    private static Guid? TryExtractOrderId(string? toolArgsJson)
    {
        if (string.IsNullOrWhiteSpace(toolArgsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(toolArgsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("orderId", out var orderIdElement)
                || !orderIdElement.TryGetGuid(out var orderId))
            {
                return null;
            }

            return orderId;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
