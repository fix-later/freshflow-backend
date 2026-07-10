using FreshFlow.Hub.Application.Commands.RecordInbound;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Mappings;

internal static class HubInboundMappings
{
    public static HubInboundDto ToDto(this HubInboundEvent inbound) =>
        new(
            inbound.Id,
            inbound.HubId,
            inbound.SourceMarketId,
            inbound.DeliveryRouteId,
            inbound.DeliveryScheduleId,
            inbound.Items.Select(item => item.ToDto()).ToList().AsReadOnly(),
            inbound.TotalQuantityKg,
            inbound.ArrivedAt,
            inbound.RecordedBy,
            inbound.HubStaffUserId,
            inbound.Status,
            inbound.ConditionStatus,
            inbound.DiscrepancyNotes,
            inbound.CreatedAt,
            inbound.UpdatedAt);

    public static HubInboundItemDto ToDto(this HubInboundItem item) =>
        new(item.MarketProductId, item.ProductId, item.QuantityKg);

    public static HubInboundItem ToDomain(this HubInboundItemCommand item) =>
        new(item.MarketProductId, item.ProductId, item.QuantityKg);
}
