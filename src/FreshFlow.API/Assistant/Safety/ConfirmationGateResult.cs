namespace FreshFlow.API.Assistant.Safety;

/// <summary>Outcome of <see cref="ConfirmationGate.Evaluate"/> for a single LLM tool call.</summary>
public enum ConfirmationDecision
{
    /// <summary>The call is not <c>confirm_order</c> — the gate has no opinion; dispatch normally.</summary>
    NotApplicable,

    /// <summary>A matching client confirmation flag was present — <c>confirm_order</c> may dispatch.</summary>
    Allowed,

    /// <summary>No matching confirmation flag — <c>confirm_order</c> must NOT dispatch; preview/ask instead.</summary>
    Blocked
}

/// <summary>
/// Result of the two-phase confirmation gate (T4). Carries the decision plus the order id the LLM was
/// trying to confirm, so a <see cref="ConfirmationDecision.Blocked"/> orchestrator turn can run
/// <c>preview_confirmation</c> for that order and surface a <c>pendingConfirmation</c> to the client.
/// </summary>
/// <param name="Decision">Whether the guarded <c>confirm_order</c> call may proceed.</param>
/// <param name="OrderId">
/// The order id parsed from the LLM's tool arguments, or null when the call carried no valid id
/// (only ever meaningful for the <c>confirm_order</c> tool).
/// </param>
/// <param name="DeliveryAddressId">Client-selected address bound to this decision.</param>
public sealed record ConfirmationGateResult(
    ConfirmationDecision Decision,
    Guid? OrderId,
    Guid? DeliveryAddressId)
{
    /// <summary>Whether the orchestrator must withhold confirmation and ask the user first.</summary>
    public bool IsBlocked => Decision == ConfirmationDecision.Blocked;

    /// <summary>The gate does not guard this tool — dispatch normally.</summary>
    public static readonly ConfirmationGateResult NotApplicable =
        new(ConfirmationDecision.NotApplicable, null, null);

    /// <summary>A matching confirmation flag was supplied — allow the confirm for <paramref name="orderId"/>.</summary>
    public static ConfirmationGateResult Allow(Guid orderId, Guid deliveryAddressId) =>
        new(ConfirmationDecision.Allowed, orderId, deliveryAddressId);

    /// <summary>No matching flag — block the confirm; <paramref name="orderId"/> (if any) is the pending order.</summary>
    public static ConfirmationGateResult Block(Guid? orderId, Guid? deliveryAddressId) =>
        new(ConfirmationDecision.Blocked, orderId, deliveryAddressId);
}
