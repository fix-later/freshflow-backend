namespace FreshFlow.API.Assistant.Dtos;

/// <summary>
/// Request body for <c>POST /api/v1/assistant/chat</c> (T5). <see cref="ConfirmOrderId"/> is the
/// second phase of the confirmation safety gate (T4): the client only ever sets it when the user
/// pressed the explicit confirm button for that exact order — the LLM cannot populate it.
/// </summary>
/// <param name="SessionId">Client-generated conversation handle; new id ⇒ fresh conversation.</param>
/// <param name="Message">The user's natural-language message for this turn.</param>
/// <param name="MarketId">Active market context for product search; carried into the session.</param>
/// <param name="DeliveryAddressId">
/// Address explicitly selected by the client. It is injected into confirmation server-side and is
/// never accepted from the LLM.
/// </param>
/// <param name="ConfirmOrderId">
/// Set only when the user explicitly confirmed an order via the UI. Must match the order the assistant
/// is about to confirm, or the <see cref="Safety.ConfirmationGate"/> blocks the confirmation.
/// </param>
public sealed record AssistantChatRequest(
    string SessionId,
    string Message,
    Guid? MarketId,
    Guid? DeliveryAddressId = null,
    Guid? ConfirmOrderId = null);
