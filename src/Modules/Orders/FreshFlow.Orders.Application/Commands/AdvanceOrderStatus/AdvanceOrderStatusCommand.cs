using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.AdvanceOrderStatus;

/// <summary>
/// Ops-driven bridge that advances a confirmed order through the pre-hub logistics
/// pipeline (Confirmed → Batched → PickedUp → AtHub) so the delivery flow (owned by
/// Logistics from AtHub onward) can run end-to-end via API. Delivering/Delivered are
/// intentionally NOT reachable here — those stay owned by Logistics delivery events.
/// </summary>
public sealed record AdvanceOrderStatusCommand(Guid OrderId, string Status) : ICommand<OrderDto>;
