using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.UpdateOrderNotes;

/// <summary>
/// Edits the order-level notes on an existing draft order, so a client can reuse the
/// restaurant's latest draft as a shopping cart instead of recreating the order.
/// </summary>
public sealed record UpdateOrderNotesCommand(
    Guid UserId,
    Guid OrderId,
    string? Notes) : ICommand<OrderDto>;
