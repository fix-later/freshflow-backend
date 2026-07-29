using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.Application.Mappings;

public static class DeliveryRouteMappings
{
    public static RouteDto ToDto(this DeliveryRoute route) =>
        new(
            route.Id,
            route.HubId,
            route.RouteType.ToString(),
            route.Status.ToString(),
            route.ServiceDate,
            route.Stops
                .Select(stop => new RouteStopDto(
                    stop.StopOrder,
                    stop.EntityType.ToString(),
                    stop.EntityId,
                    stop.EntityName,
                    stop.Latitude,
                    stop.Longitude,
                    stop.EstimatedArrivalAt,
                    stop.EstimatedDepartureAt))
                .ToList()
                .AsReadOnly(),
            route.TotalDistanceKm,
            route.EstimatedDurationMinutes,
            route.EstimatedCost,
            route.OptimizationCriteria?.ToString(),
            route.VehicleId,
            route.DriverUserId,
            route.OrderGroupId,
            route.CreatedAt,
            route.UpdatedAt);
}
