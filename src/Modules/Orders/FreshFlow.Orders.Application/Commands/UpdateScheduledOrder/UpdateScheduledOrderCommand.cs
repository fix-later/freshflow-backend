using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.UpdateScheduledOrder;

/// <summary>
/// SCRUM-201 — UC-ORD-10: update a recurring scheduled order template.
/// SCRUM-386 — Items/DeliveryAddressId are optional, matching the other fields' "omit to keep
/// unchanged" semantics; when provided, Items wholesale-replaces the item template.
/// </summary>
public sealed record UpdateScheduledOrderCommand(
    Guid UserId,
    bool IsAdmin,
    Guid ScheduledOrderId,
    string? RecurrenceType,
    DateTime? FirstRunAt,
    string? Notes,
    Guid? DeliveryAddressId = null,
    IReadOnlyList<DraftOrderItemRequest>? Items = null) : ICommand<ScheduledOrderDto>;
