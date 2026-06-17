using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.UpdateOrderItem;

/// <summary>
/// SCRUM-186 — UC-ORD-03: Update Draft Order Item.
/// Edits the quantity of an existing line item on a draft order.
/// </summary>
public sealed record UpdateOrderItemCommand(
    Guid UserId,
    Guid OrderId,
    Guid OrderItemId,
    int Quantity) : ICommand<OrderDto>;
