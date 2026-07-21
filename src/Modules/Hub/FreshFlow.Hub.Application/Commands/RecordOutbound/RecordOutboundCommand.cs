using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.RecordOutbound;

public sealed record RecordOutboundCommand(
    Guid HubId,
    Guid DestinationRouteId,
    IReadOnlyList<HubOutboundItemCommand> Items,
    DateTime DispatchedAt,
    Guid ActorUserId = default,
    bool BypassHubAssignment = false)
    : ICommand<HubOutboundEventDto>, IHubAccessRequest;

public sealed record HubOutboundItemCommand(
    Guid MarketProductId,
    Guid? ProductId,
    decimal QuantityKg);
