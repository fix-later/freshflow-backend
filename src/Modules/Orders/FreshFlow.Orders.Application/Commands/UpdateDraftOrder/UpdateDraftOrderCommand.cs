using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.UpdateDraftOrder;

/// <summary>
/// Edits the notes and/or delivery date on an existing draft order, so a client can reuse the
/// restaurant's latest draft as a shopping cart instead of recreating the order. Both fields are
/// set to the given values (full replace), matching how order items are edited.
/// </summary>
public sealed record UpdateDraftOrderCommand(
    Guid UserId,
    Guid OrderId,
    string? Notes,
    DateTime? ScheduledFor) : ICommand<OrderDto>;
