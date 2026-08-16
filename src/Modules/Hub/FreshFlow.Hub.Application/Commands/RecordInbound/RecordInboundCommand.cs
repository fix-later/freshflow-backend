using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.RecordInbound;

public sealed record RecordInboundCommand(
    Guid HubId,
    Guid? SourceMarketId,
    Guid? DeliveryScheduleId,
    IReadOnlyList<HubInboundItemCommand> Items,
    DateTime ArrivedAt,
    Guid ActorUserId = default,
    bool BypassHubAssignment = false)
    : ICommand<HubInboundDto>, IHubAccessRequest;

public sealed record HubInboundItemCommand(
    Guid MarketProductId,
    Guid? ProductId,
    decimal QuantityKg,
    string? ProductName = null);
