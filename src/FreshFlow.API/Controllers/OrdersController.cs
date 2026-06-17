using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Orders.Application.Commands.AddOrderItem;
using FreshFlow.Orders.Application.Commands.CreateDraftOrder;
using FreshFlow.Orders.Application.Commands.RemoveOrderItem;
using FreshFlow.Orders.Application.Commands.UpdateOrderItem;
using FreshFlow.Orders.Application.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize(Roles = "restaurant")]
[EnableRateLimiting("orders")]
public sealed class OrdersController(ISender sender) : ControllerBase
{
    /// <summary>POST /api/v1/orders — UC-ORD-01: creates a draft order with one or more line items.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDraftOrderAsync(
        [FromBody] CreateDraftOrderRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateDraftOrderCommand(ResolveUserId(), body.Items, body.ScheduledFor, body.Notes), ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(CreateDraftOrderAsync), null, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    /// <summary>POST /api/v1/orders/{orderId}/items — UC-ORD-02: adds an item to a draft order.</summary>
    [HttpPost("{orderId:guid}/items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddOrderItemAsync(
        Guid orderId, [FromBody] AddOrderItemRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new AddOrderItemCommand(ResolveUserId(), orderId, body.MarketProductId, body.Quantity), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PUT /api/v1/orders/{orderId}/items/{itemId} — UC-ORD-03: updates a draft order item's quantity.</summary>
    [HttpPut("{orderId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateOrderItemAsync(
        Guid orderId, Guid itemId, [FromBody] UpdateOrderItemRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateOrderItemCommand(ResolveUserId(), orderId, itemId, body.Quantity), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>DELETE /api/v1/orders/{orderId}/items/{itemId} — UC-ORD-04: removes an item from a draft order.</summary>
    [HttpDelete("{orderId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveOrderItemAsync(Guid orderId, Guid itemId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveOrderItemCommand(ResolveUserId(), orderId, itemId), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        return Guid.TryParse(raw, out var id)
            ? id
            : throw new UnauthorizedAccessException("User ID claim is missing or malformed.");
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record CreateDraftOrderRequest(
    IReadOnlyList<DraftOrderItemRequest> Items,
    DateTime? ScheduledFor,
    string? Notes);

public sealed record AddOrderItemRequest(Guid MarketProductId, int Quantity);

public sealed record UpdateOrderItemRequest(int Quantity);
