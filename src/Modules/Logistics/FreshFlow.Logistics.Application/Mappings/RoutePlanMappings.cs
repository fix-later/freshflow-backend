using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.Application.Mappings;

public static class RoutePlanMappings
{
    public static RoutePlanDto ToDto(
        this RoutePlan plan,
        IReadOnlyList<DeliveryRoute> routes,
        IReadOnlyDictionary<Guid, PlanningVehicle> vehicles)
    {
        var plannedRoutes = routes.Select(route =>
        {
            var vehicle = vehicles[route.SuggestedVehicleId!.Value];
            var load = route.PlannedLoadKg ?? 0m;
            return new PlannedRouteDto(
                route.Id, vehicle.Id, vehicle.PlateNumber, vehicle.VehicleType.ToString(),
                load, vehicle.EffectiveCapacityKg,
                vehicle.EffectiveCapacityKg == 0 ? 0 : Math.Round(load / vehicle.EffectiveCapacityKg * 100m, 2),
                route.TotalDistanceKm ?? 0m, route.EstimatedDurationMinutes ?? 0,
                route.EstimatedCost ?? 0m, route.EstimatedReturnAt!.Value,
                route.Stops.Where(stop => stop.OrderIds is not null)
                    .Select(stop => new PlannedRouteStopDto(
                        stop.StopOrder, stop.EntityId, stop.EntityName,
                        stop.OrderIds!, stop.LoadKg ?? 0m,
                        stop.EstimatedArrivalAt, stop.EstimatedDepartureAt))
                    .ToList());
        }).ToList();

        return new RoutePlanDto(
            plan.Id, plan.Status.ToString(), plan.HubId, plan.ServiceDate,
            plan.OptimizationCriteria.ToString().ToUpperInvariant(), plan.RoutingProvider,
            plan.IsEstimated, plan.InputRevision, plan.VehiclesUsed, plan.TotalLoadKg,
            plan.TotalDistanceKm, plan.EstimatedDurationMinutes, plan.EstimatedCost,
            plannedRoutes,
            plan.Unassigned.Select(x => new UnassignedRouteDemandDto(
                x.RestaurantId, x.RestaurantName, x.OrderIds, x.LoadKg, x.Reason)).ToList(),
            plan.IsEstimated ? ["Routing uses estimated Haversine road distances."] : [],
            plan.CreatedAt);
    }
}
