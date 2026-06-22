namespace FreshFlow.API.Assistant.Dtos;

/// <summary>
/// Response body for <c>POST /api/v1/assistant/chat</c> (T5, non-streaming v1).
/// </summary>
/// <param name="Reply">The assistant's natural-language reply to show the user.</param>
/// <param name="SessionId">Echoes the conversation handle so the client can continue the session.</param>
/// <param name="PendingConfirmation">
/// Present when the assistant prepared an order that now needs the user's explicit confirmation. The
/// client renders a confirm button that, when pressed, re-calls <c>chat</c> with
/// <c>ConfirmOrderId = PendingConfirmation.OrderId</c>.
/// </param>
/// <param name="DraftOrderId">The draft order built/updated during this turn, if any.</param>
public sealed record AssistantChatResponse(
    string Reply,
    string SessionId,
    PendingConfirmation? PendingConfirmation = null,
    Guid? DraftOrderId = null);

/// <summary>
/// Signals that the assistant has an order ready to confirm but is withholding the confirmation until
/// the user approves it explicitly (two-phase gate, T4).
/// </summary>
/// <param name="OrderId">The order awaiting the user's explicit confirm.</param>
/// <param name="PreviewJson">
/// Raw JSON of the confirmation preview (credit/cutoff checks) the assistant ran, for the client to
/// render the confirmation summary. Sensitive fields stripped by the tool-result mapper.
/// </param>
public sealed record PendingConfirmation(Guid OrderId, string PreviewJson);
