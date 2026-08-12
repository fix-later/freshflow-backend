using System.Security.Claims;
using FluentValidation;
using FreshFlow.API.Assistant;
using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.API.Assistant.Conversation;
using FreshFlow.API.Assistant.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FreshFlow.API.Controllers;

/// <summary>
/// AI shopping assistant chat endpoint (Tầng 2 — T5). Restaurant users send natural-language messages;
/// the orchestrator drives the LLM ↔ tool loop and returns a reply plus any pending-confirmation /
/// draft-order signals. Confirming an order is two-phase: the assistant never confirms on its own — the
/// client re-calls with <c>confirmOrderId</c> only after the user presses the confirm button.
/// </summary>
[ApiController]
[Route("api/v1/assistant")]
[Authorize(Roles = "restaurant")]
[EnableRateLimiting("assistant")]
public sealed class AssistantController(
    AssistantOrchestrator orchestrator,
    IConversationStore conversationStore,
    IValidator<AssistantChatRequest> requestValidator,
    ILogger<AssistantController> logger) : ControllerBase
{
    /// <summary>POST /api/v1/assistant/chat — one conversational turn against the shopping assistant.</summary>
    [HttpPost("chat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> ChatAsync([FromBody] AssistantChatRequest request, CancellationToken ct)
    {
        // H1: validated explicitly — the chat endpoint calls the orchestrator directly, not via ISender,
        // so MediatR's ValidationBehavior never runs for this request.
        var validation = await requestValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse.Err("INVALID_REQUEST", validation.Errors[0].ErrorMessage));
        }

        var userId = ResolveUserId();

        var existing = await conversationStore.LoadAsync(request.SessionId, ct);

        // C1 (CWE-639): sessionId is a client-generated handle, not a server-issued secret, so it can be
        // leaked or guessed. Never resume — or even acknowledge — a conversation owned by a different
        // user: that would let the caller read another user's reply/draft and, with a matching
        // confirmOrderId, confirm that user's order. 404 (not 403) so we don't reveal the session exists.
        if (existing is not null && existing.UserId != userId)
        {
            return NotFound(ApiResponse.Err("SESSION_NOT_FOUND", "Conversation session not found."));
        }

        var state = BuildState(existing, request, userId);
        state = state with { Turns = [.. state.Turns, new ConversationTurn(ConversationRole.User, request.Message)] };

        AssistantTurnOutcome outcome;
        try
        {
            outcome = await orchestrator.RunAsync(
                state, request.ConfirmOrderId, request.DeliveryAddressId, ct);
        }
        catch (AssistantProviderException ex)
        {
            logger.LogWarning(ex, "Assistant provider request failed: {Failure}", ex.Failure);
            return ProviderFailure(ex.Failure);
        }

        if (!await conversationStore.SaveAsync(outcome.State, ct))
        {
            return NotFound(ApiResponse.Err("SESSION_NOT_FOUND", "Conversation session not found."));
        }

        return Ok(ApiResponse.Ok(new AssistantChatResponse(
            outcome.Reply,
            request.SessionId,
            outcome.PendingConfirmation,
            outcome.DraftOrderId,
            outcome.CreditSummary,
            outcome.DeliveryAddresses)));
    }

    /// <summary>
    /// Resumes the stored conversation, or starts a fresh one. Precondition (enforced by the caller):
    /// <paramref name="existing"/> is either null or already owned by <paramref name="userId"/>, so a
    /// non-null state is always safe to resume here. The market context always follows the latest
    /// request (if supplied); user identity always comes from the JWT, never the request body.
    /// </summary>
    private static ConversationState BuildState(ConversationState? existing, AssistantChatRequest request, Guid userId)
    {
        if (existing is null)
        {
            return new ConversationState(
                request.SessionId,
                userId,
                request.MarketId,
                Turns: []);
        }

        return request.MarketId is null ? existing : existing with { MarketId = request.MarketId };
    }

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        return Guid.TryParse(raw, out var id)
            ? id
            : throw new UnauthorizedAccessException("User ID claim is missing or malformed.");
    }

    internal static ObjectResult ProviderFailure(AssistantProviderFailure failure) => failure switch
    {
        AssistantProviderFailure.AuthenticationFailed => Error(
            StatusCodes.Status502BadGateway,
            "ASSISTANT_PROVIDER_AUTH_FAILED",
            "AI provider authentication failed."),
        AssistantProviderFailure.RateLimited => Error(
            StatusCodes.Status429TooManyRequests,
            "ASSISTANT_PROVIDER_RATE_LIMITED",
            "AI provider usage limit has been reached. Please try again later."),
        AssistantProviderFailure.Timeout => Error(
            StatusCodes.Status504GatewayTimeout,
            "ASSISTANT_PROVIDER_TIMEOUT",
            "AI provider timed out. Please try again."),
        _ => Error(
            StatusCodes.Status502BadGateway,
            "ASSISTANT_PROVIDER_UNAVAILABLE",
            "AI provider is unavailable. Please try again later.")
    };

    private static ObjectResult Error(int statusCode, string code, string message) =>
        new(ApiResponse.Err(code, message)) { StatusCode = statusCode };
}
