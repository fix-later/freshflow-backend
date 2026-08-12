using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.Application.Mappings;

internal static class VehicleMappings
{
    public static VehicleDto ToDto(this Vehicle vehicle) =>
        new(
            vehicle.Id,
            vehicle.PlateNumber,
            vehicle.CapacityKg,
            vehicle.VehicleType.ToString(),
            vehicle.IsAvailable,
            vehicle.DeletedAt is null,
            vehicle.CreatedAt,
            vehicle.UpdatedAt,
            vehicle.HubId);
}
