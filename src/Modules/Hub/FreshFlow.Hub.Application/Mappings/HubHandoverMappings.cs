using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Mappings;

internal static class HubHandoverMappings
{
    public static HubHandoverDto ToDto(this HubHandoverEvent handover) =>
        new(
            handover.Id,
            handover.HubId,
            handover.DeliveryRouteId,
            handover.DriverUserId,
            handover.OutboundEventId,
            handover.Status,
            handover.HandedOverBy,
            handover.HandedOverAt,
            handover.DriverConfirmedAt,
            handover.Notes,
            handover.CreatedAt,
            handover.UpdatedAt);
}
