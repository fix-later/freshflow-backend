using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.Application.Mappings;

internal static class DeliveryZoneMappings
{
    public static DeliveryZoneDto ToDto(this DeliveryZone zone) =>
        new(
            zone.Id,
            zone.Code,
            zone.Name,
            zone.Description,
            zone.IsActive,
            zone.CreatedAt,
            zone.UpdatedAt);
}
