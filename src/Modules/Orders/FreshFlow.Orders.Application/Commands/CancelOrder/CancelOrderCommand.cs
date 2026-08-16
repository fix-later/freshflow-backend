using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.CancelOrder;

/// <summary>
/// SCRUM-214 — UC-ORD-15: Cancel an order before batching.
/// </summary>
public sealed record CancelOrderCommand(
    Guid UserId,
    bool IsAdmin,
    Guid OrderId,
    string? Reason) : ICommand<OrderDto>;
