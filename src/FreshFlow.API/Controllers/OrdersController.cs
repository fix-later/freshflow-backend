using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Orders.Application.Commands.AddOrderItem;
using FreshFlow.Orders.Application.Commands.CancelOrder;
using FreshFlow.Orders.Application.Commands.CancelScheduledOrder;
using FreshFlow.Orders.Application.Commands.ConfirmOrder;
using FreshFlow.Orders.Application.Commands.CreateDraftOrder;
using FreshFlow.Orders.Application.Commands.CreateScheduledOrder;
using FreshFlow.Orders.Application.Commands.RecordOrderItemActualQuantity;
using FreshFlow.Orders.Application.Commands.RemoveOrderItem;
using FreshFlow.Orders.Application.Commands.UpdateOrderItem;
using FreshFlow.Orders.Application.Commands.UpdateScheduledOrder;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Queries.GetOrder;
using FreshFlow.Orders.Application.Queries.GetScheduledOrder;
using FreshFlow.Orders.Application.Queries.ListOrders;
using FreshFlow.Orders.Application.Queries.ListScheduledOrderInstances;
using FreshFlow.Orders.Application.Queries.ListScheduledOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize(Roles = "admin,operations_manager,restaurant")]
[EnableRateLimiting("orders")]
public sealed class OrdersController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/orders — UC-ORD-12/20: lists orders/history with pagination and filters.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> ListOrdersAsync(
        [FromQuery] Guid? restaurantId,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? sort = "createdAt:desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        ListOrdersInternalAsync(restaurantId, status, from, to, sort, page, pageSize, ct);

    /// <summary>GET /api/v1/orders/history — UC-ORD-20 alias over the same order-list filters.</summary>
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetOrderHistoryAsync(
        [FromQuery] Guid? restaurantId,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? sort = "createdAt:desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        ListOrdersInternalAsync(restaurantId, status, from, to, sort, page, pageSize, ct);

    /// <summary>GET /api/v1/orders/scheduled — UC-ORD-10: lists recurring scheduled orders.</summary>
    [HttpGet("scheduled")]
    [Authorize(Roles = "admin,restaurant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListScheduledOrdersAsync(
        [FromQuery] Guid? restaurantId,
        [FromQuery] bool includeCancelled = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListScheduledOrdersQuery(
                ResolveUserId(),
                User.IsInRole("admin"),
                restaurantId,
                includeCancelled,
                page,
                pageSize),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>GET /api/v1/orders/scheduled/{scheduledOrderId} — UC-ORD-10: recurring schedule detail.</summary>
    [HttpGet("scheduled/{scheduledOrderId:guid}")]
    [Authorize(Roles = "admin,restaurant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScheduledOrderAsync(Guid scheduledOrderId, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetScheduledOrderQuery(ResolveUserId(), User.IsInRole("admin"), scheduledOrderId), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// GET /api/v1/orders/scheduled/{scheduledOrderId}/instances — UC-ORD-11:
    /// lists generated concrete order instances.
    /// </summary>
    [HttpGet("scheduled/{scheduledOrderId:guid}/instances")]
    [Authorize(Roles = "admin,restaurant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListScheduledOrderInstancesAsync(
        Guid scheduledOrderId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListScheduledOrderInstancesQuery(
                ResolveUserId(),
                User.IsInRole("admin"),
                scheduledOrderId,
                page,
                pageSize),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>GET /api/v1/orders/{orderId} — UC-ORD-13: returns an order with line items.</summary>
    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderAsync(Guid orderId, CancellationToken ct)
    {
        var result = await sender.Send(new GetOrderQuery(ResolveUserId(), CanReadAllOrders(), orderId), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>POST /api/v1/orders — UC-ORD-01: creates a draft order with one or more line items.</summary>
    [HttpPost]
    [Authorize(Roles = "restaurant")]
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

    /// <summary>POST /api/v1/orders/scheduled — UC-ORD-09: creates a recurring scheduled order.</summary>
    [HttpPost("scheduled")]
    [Authorize(Roles = "restaurant")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateScheduledOrderAsync(
        [FromBody] CreateScheduledOrderRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateScheduledOrderCommand(
                ResolveUserId(),
                body.RecurrenceType,
                body.FirstRunAt,
                body.Notes),
            ct);

        return result.IsSuccess
            ? CreatedAtAction(
                nameof(GetScheduledOrderAsync),
                new { scheduledOrderId = result.Value.ScheduledOrderId },
                ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    /// <summary>POST /api/v1/orders/{orderId}/items — UC-ORD-02: adds an item to a draft order.</summary>
    [HttpPost("{orderId:guid}/items")]
    [Authorize(Roles = "restaurant")]
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
    [Authorize(Roles = "restaurant")]
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
    [Authorize(Roles = "restaurant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveOrderItemAsync(Guid orderId, Guid itemId, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveOrderItemCommand(ResolveUserId(), orderId, itemId), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// POST /api/v1/orders/{orderId}/confirm — UC-ORD-06/07/08: confirms a draft order,
    /// checks B2B credit, locks item prices, applies the 22:00 cutoff, and charges credit.
    /// </summary>
    [HttpPost("{orderId:guid}/confirm")]
    [Authorize(Roles = "restaurant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmOrderAsync(Guid orderId, CancellationToken ct)
    {
        var result = await sender.Send(new ConfirmOrderCommand(ResolveUserId(), orderId), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PATCH /api/v1/orders/{orderId}/cancel — UC-ORD-15: cancels a draft/confirmed order.</summary>
    [HttpPatch("{orderId:guid}/cancel")]
    [Authorize(Roles = "admin,restaurant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CancelOrderAsync(
        Guid orderId, [FromBody] CancelOrderRequest? body, CancellationToken ct)
    {
        var result = await sender.Send(
            new CancelOrderCommand(ResolveUserId(), User.IsInRole("admin"), orderId, body?.Reason), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// PATCH /api/v1/orders/{orderId}/items/{itemId}/actual-quantity — UC-ORD-16/17:
    /// records fulfilled quantity after shortage/damage adjustment.
    /// </summary>
    [HttpPatch("{orderId:guid}/items/{itemId:guid}/actual-quantity")]
    [Authorize(Roles = "admin,operations_manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordOrderItemActualQuantityAsync(
        Guid orderId, Guid itemId, [FromBody] RecordActualQuantityRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new RecordOrderItemActualQuantityCommand(ResolveUserId(), orderId, itemId, body.ActualQuantity), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PATCH /api/v1/orders/scheduled/{scheduledOrderId} — UC-ORD-10: updates a recurring schedule.</summary>
    [HttpPatch("scheduled/{scheduledOrderId:guid}")]
    [Authorize(Roles = "admin,restaurant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateScheduledOrderAsync(
        Guid scheduledOrderId, [FromBody] UpdateScheduledOrderRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateScheduledOrderCommand(
                ResolveUserId(),
                User.IsInRole("admin"),
                scheduledOrderId,
                body.RecurrenceType,
                body.FirstRunAt,
                body.Notes),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PATCH /api/v1/orders/scheduled/{scheduledOrderId}/cancel — UC-ORD-10: cancels a recurring schedule.</summary>
    [HttpPatch("scheduled/{scheduledOrderId:guid}/cancel")]
    [Authorize(Roles = "admin,restaurant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelScheduledOrderAsync(Guid scheduledOrderId, CancellationToken ct)
    {
        var result = await sender.Send(
            new CancelScheduledOrderCommand(ResolveUserId(), User.IsInRole("admin"), scheduledOrderId), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<IActionResult> ListOrdersInternalAsync(
        Guid? restaurantId,
        string? status,
        DateTime? from,
        DateTime? to,
        string? sort,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ListOrdersQuery(
                ResolveUserId(),
                CanReadAllOrders(),
                restaurantId,
                status,
                from,
                to,
                sort,
                page,
                pageSize),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    private bool CanReadAllOrders() =>
        User.IsInRole("admin") || User.IsInRole("operations_manager");

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

public sealed record CancelOrderRequest(string? Reason);

public sealed record RecordActualQuantityRequest(decimal ActualQuantity);

public sealed record CreateScheduledOrderRequest(
    string RecurrenceType,
    DateTime FirstRunAt,
    string? Notes);

public sealed record UpdateScheduledOrderRequest(
    string? RecurrenceType,
    DateTime? FirstRunAt,
    string? Notes);
