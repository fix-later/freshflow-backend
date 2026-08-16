using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.CreateDraftOrder;

/// <summary>
/// SCRUM-180 — UC-ORD-01: Create Draft Order.
/// Creates a new order in Draft status with one or more line items for the authenticated
/// restaurant. Each item is enriched server-side with live price/name from market_products.
/// </summary>
/// <param name="UserId">Authenticated user (from JWT claim); resolved to a restaurant.</param>
/// <param name="Items">Requested line items (marketProductId + quantity).</param>
/// <param name="ScheduledFor">Optional desired delivery time.</param>
/// <param name="Notes">Optional order-level notes.</param>
public sealed record CreateDraftOrderCommand(
    Guid UserId,
    IReadOnlyList<DraftOrderItemRequest> Items,
    DateTime? ScheduledFor,
    string? Notes) : ICommand<OrderDto>;
