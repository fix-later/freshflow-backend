using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.CreateScheduledOrder;

/// <summary>
/// SCRUM-198 — UC-ORD-09: create a recurring scheduled order template.
/// </summary>
public sealed record CreateScheduledOrderCommand(
    Guid UserId,
    string RecurrenceType,
    DateTime FirstRunAt,
    string? Notes) : ICommand<ScheduledOrderDto>;
