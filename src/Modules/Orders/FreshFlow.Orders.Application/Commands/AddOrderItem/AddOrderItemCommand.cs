using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.AddOrderItem;

/// <summary>
/// SCRUM-183 — UC-ORD-02: Add Item to Draft Order.
/// </summary>
/// <param name="UserId">Authenticated user (from JWT claim); resolved to a restaurant.</param>
/// <param name="OrderId">Draft order being edited.</param>
/// <param name="MarketProductId">Product to add.</param>
/// <param name="Quantity">Requested quantity.</param>
public sealed record AddOrderItemCommand(
    Guid UserId,
    Guid OrderId,
    Guid MarketProductId,
    int Quantity) : ICommand<OrderDto>;
