using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.RemoveOrderItem;

/// <summary>
/// SCRUM-189 — UC-ORD-04: Remove Item from Draft Order.
/// </summary>
public sealed record RemoveOrderItemCommand(
    Guid UserId,
    Guid OrderId,
    Guid OrderItemId) : ICommand<OrderDto>;
