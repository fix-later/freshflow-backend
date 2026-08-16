namespace FreshFlow.API.Assistant.Dtos;

/// <summary>
/// Response body for <c>POST /api/v1/assistant/chat</c> (T5, non-streaming v1).
/// </summary>
/// <param name="Reply">The assistant's natural-language reply to show the user.</param>
/// <param name="SessionId">Echoes the conversation handle so the client can continue the session.</param>
/// <param name="PendingConfirmation">
/// Present when the assistant prepared an order that now needs the user's explicit confirmation. The
/// client renders a confirm button that, when pressed, re-calls <c>chat</c> with
/// <c>ConfirmOrderId = PendingConfirmation.OrderId</c> while echoing the pending
/// <c>DeliveryAddressId</c>.
/// </param>
/// <param name="DraftOrderId">The draft order built/updated during this turn, if any.</param>
/// <param name="CreditSummary">Current restaurant credit, returned directly to the client and never sent to the LLM.</param>
/// <param name="DeliveryAddresses">Restaurant delivery addresses, returned directly to the client and never sent to the LLM.</param>
public sealed record AssistantChatResponse(
    string Reply,
    string SessionId,
    PendingConfirmation? PendingConfirmation = null,
    Guid? DraftOrderId = null,
    CreditSummary? CreditSummary = null,
    IReadOnlyList<AssistantDeliveryAddress>? DeliveryAddresses = null);

/// <summary>
/// Signals that the assistant has an order ready to confirm but is withholding the confirmation until
/// the user approves it explicitly (two-phase gate, T4).
/// </summary>
/// <param name="OrderId">The order awaiting the user's explicit confirm.</param>
/// <param name="DeliveryAddressId">The client-selected address included in the confirmation.</param>
/// <param name="PreviewJson">
/// Raw JSON of the confirmation preview (credit/cutoff checks) the assistant ran, for the client to
/// render the confirmation summary. Sensitive fields stripped by the tool-result mapper.
/// </param>
public sealed record PendingConfirmation(Guid OrderId, Guid DeliveryAddressId, string PreviewJson);

public sealed record CreditSummary(
    decimal CreditLimit,
    decimal OutstandingBalance,
    decimal AvailableCredit,
    DateTime UpdatedAt);

public sealed record AssistantDeliveryAddress(
    Guid Id,
    string? RecipientName,
    string? Phone,
    string AddressLine,
    bool IsDefault);
