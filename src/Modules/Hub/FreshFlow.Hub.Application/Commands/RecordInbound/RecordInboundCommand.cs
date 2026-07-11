using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.RecordInbound;

public sealed record RecordInboundCommand(
    Guid HubId,
    Guid? SourceMarketId,
    Guid? DeliveryScheduleId,
    IReadOnlyList<HubInboundItemCommand> Items,
    DateTime ArrivedAt) : ICommand<HubInboundDto>;

public sealed record HubInboundItemCommand(
    Guid MarketProductId,
    Guid? ProductId,
    decimal QuantityKg);
