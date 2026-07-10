using FreshFlow.Hub.Application.Commands.RecordOutbound;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Mappings;

internal static class HubDispatchMappings
{
    public static CrossDockTransferDto ToDto(this CrossDockTransfer transfer) =>
        new(
            transfer.Id,
            transfer.HubId,
            transfer.InboundEventId,
            transfer.OutboundRouteId,
            transfer.Status,
            transfer.Notes,
            transfer.CreatedAt,
            transfer.UpdatedAt);

    public static HubOutboundEventDto ToDto(this HubOutboundEvent outbound) =>
        new(
            outbound.Id,
            outbound.HubId,
            outbound.DestinationRouteId,
            outbound.Items.Select(item => item.ToDto()).ToList().AsReadOnly(),
            outbound.TotalQuantityKg,
            outbound.DispatchedAt,
            outbound.RecordedBy,
            outbound.CreatedAt,
            outbound.UpdatedAt);

    public static HubOutboundItemDto ToDto(this HubOutboundItem item) =>
        new(item.MarketProductId, item.ProductId, item.QuantityKg);

    public static HubOutboundItem ToDomain(this HubOutboundItemCommand item) =>
        new(item.MarketProductId, item.ProductId, item.QuantityKg);
}
