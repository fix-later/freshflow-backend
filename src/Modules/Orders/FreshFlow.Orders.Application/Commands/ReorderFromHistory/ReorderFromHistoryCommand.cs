using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.ReorderFromHistory;

/// <summary>
/// SCRUM-231 — UC-ORD-21: creates a new draft order from a previous order's line items.
/// </summary>
public sealed record ReorderFromHistoryCommand(
    Guid UserId,
    Guid SourceOrderId,
    DateTime? ScheduledFor,
    string? Notes) : ICommand<OrderDto>;
