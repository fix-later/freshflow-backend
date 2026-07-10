using FreshFlow.Hub.Application.Dtos;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Application.Mappings;

internal static class HubMappings
{
    public static HubDto ToDto(this HubEntity hub) =>
        new(
            hub.Id,
            hub.Name,
            hub.Address,
            hub.Latitude,
            hub.Longitude,
            hub.CapacityKg,
            hub.OccupiedCapacityKg,
            hub.CapacityKg - hub.OccupiedCapacityKg,
            hub.IsActive,
            hub.ManagedBy,
            hub.CreatedAt,
            hub.UpdatedAt);
}
