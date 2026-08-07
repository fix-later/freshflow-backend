using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.CreateScheduledOrder;

/// <summary>
/// SCRUM-198 — UC-ORD-09: create a recurring scheduled order template.
/// SCRUM-386 — Items + DeliveryAddressId let the background job auto-place a real confirmed
/// order at run time instead of an empty Draft.
/// </summary>
public sealed record CreateScheduledOrderCommand(
    Guid UserId,
    string RecurrenceType,
    DateTime FirstRunAt,
    string? Notes,
    Guid DeliveryAddressId,
    IReadOnlyList<DraftOrderItemRequest> Items) : ICommand<ScheduledOrderDto>;
