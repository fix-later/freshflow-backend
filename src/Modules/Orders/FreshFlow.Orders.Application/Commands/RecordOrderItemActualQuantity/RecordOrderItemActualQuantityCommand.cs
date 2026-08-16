using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.RecordOrderItemActualQuantity;

/// <summary>
/// SCRUM-217/SCRUM-220 — UC-ORD-16/17: record fulfilled quantity after shortage/damage adjustment.
/// </summary>
public sealed record RecordOrderItemActualQuantityCommand(
    Guid UserId,
    Guid OrderId,
    Guid OrderItemId,
    decimal ActualQuantity) : ICommand<OrderDto>;
