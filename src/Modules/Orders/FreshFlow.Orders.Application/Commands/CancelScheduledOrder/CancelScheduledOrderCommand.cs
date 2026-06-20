using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.CancelScheduledOrder;

/// <summary>
/// SCRUM-201 — UC-ORD-10: cancel a recurring scheduled order template.
/// </summary>
public sealed record CancelScheduledOrderCommand(
    Guid UserId,
    bool IsAdmin,
    Guid ScheduledOrderId) : ICommand<ScheduledOrderDto>;
