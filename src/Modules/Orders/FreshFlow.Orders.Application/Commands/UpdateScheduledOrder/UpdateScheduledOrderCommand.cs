using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.UpdateScheduledOrder;

/// <summary>
/// SCRUM-201 — UC-ORD-10: update a recurring scheduled order template.
/// </summary>
public sealed record UpdateScheduledOrderCommand(
    Guid UserId,
    bool IsAdmin,
    Guid ScheduledOrderId,
    string? RecurrenceType,
    DateTime? FirstRunAt,
    string? Notes) : ICommand<ScheduledOrderDto>;
