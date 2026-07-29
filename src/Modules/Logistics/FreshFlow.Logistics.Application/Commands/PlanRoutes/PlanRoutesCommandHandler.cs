using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Common;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.PlanRoutes;

internal sealed class PlanRoutesCommandHandler(
    IHubCoordinateReader hubs,
    IRestaurantCoordinateReader restaurants,
    IOrderStatusReader orders,
    IOrderPackingReader packing,
    IVehicleRepository vehicles,
    IVehicleCapacityPolicy capacityPolicy,
    IRouteOptimizer optimizer,
    IDeliveryRouteRepository routes)
    : IRequestHandler<PlanRoutesCommand, Result<IReadOnlyList<RouteDto>>>
{
    public async Task<Result<IReadOnlyList<RouteDto>>> Handle(
        PlanRoutesCommand request,
        CancellationToken ct)
    {
        var requestedCriteria = string.IsNullOrWhiteSpace(request.OptimizationCriteria)
            ? nameof(OptimizationCriteria.distance)
            : request.OptimizationCriteria;
        if (!Enum.TryParse<OptimizationCriteria>(
                requestedCriteria,
                ignoreCase: true,
                out var criteria))
        {
            return Result<IReadOnlyList<RouteDto>>.Failure(Error.Validation(
                "VALIDATION_ERROR",
                "OptimizationCriteria must be DISTANCE, TIME, or COST."));
        }

        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<IReadOnlyList<RouteDto>>.Failure(Error.NotFound("HUB", request.HubId));
        if (hub.Latitude is null || hub.Longitude is null)
        {
            return Result<IReadOnlyList<RouteDto>>.Failure(Error.Validation(
                "MISSING_COORDINATES",
                $"Hub '{request.HubId}' has no coordinates configured."));
        }

        var routable = await orders.ListRoutableRestaurantsAsync(
            request.ServiceDate,
            ["AtHub"],
            ct);
        var candidateIds = routable.Select(item => item.RestaurantId).ToList();
        var atHubOrders = await orders.ListByRestaurantsAndStatusAsync(
            candidateIds,
            "AtHub",
            ct,
            request.HubId,
            request.ServiceDate);
        var restaurantIds = atHubOrders
            .Select(order => order.RestaurantId)
            .Distinct()
            .ToList();
        if (restaurantIds.Count == 0)
            return Result<IReadOnlyList<RouteDto>>.Success([]);

        var coordinates = new Dictionary<Guid, RestaurantCoordinateDto>(restaurantIds.Count);
        var missingCoordinates = new List<Guid>();
        foreach (var restaurantId in restaurantIds)
        {
            var restaurant = await restaurants.FindByRestaurantIdAsync(restaurantId, ct);
            if (restaurant?.Latitude is null || restaurant.Longitude is null)
            {
                missingCoordinates.Add(restaurantId);
                continue;
            }

            coordinates[restaurantId] = restaurant;
        }

        if (missingCoordinates.Count > 0)
        {
            return Result<IReadOnlyList<RouteDto>>.Failure(Error.Validation(
                "MISSING_COORDINATES",
                $"Restaurants have no coordinates configured: {string.Join(", ", missingCoordinates)}."));
        }

        var packingByOrder = await packing.GetLinesByOrdersAsync(
            atHubOrders.Select(order => order.OrderId).ToList(),
            ct);
        var restaurantByOrder = atHubOrders.ToDictionary(order => order.OrderId, order => order.RestaurantId);
        var loadByRestaurant = restaurantIds.ToDictionary(id => id, _ => 0m);
        foreach (var orderPacking in packingByOrder)
        {
            if (!restaurantByOrder.TryGetValue(orderPacking.OrderId, out var restaurantId))
                continue;

            loadByRestaurant[restaurantId] += orderPacking.Lines.Sum(line =>
                line.CapacityKg is { } capacityKg && capacityKg > 0m
                    ? line.Quantity * capacityKg
                    : 0m);
        }

        var (fleet, _) = await vehicles.GetPageAsync(null, 10_000, true, ct);
        var maxStops = Math.Min(capacityPolicy.MaxStopsPerVehicle, 20);
        var plan = CapacitatedSweepPlanner.Plan(
            hub.Latitude.Value,
            hub.Longitude.Value,
            restaurantIds.Select(id => new SweepRestaurant(
                    id,
                    coordinates[id].Latitude!.Value,
                    coordinates[id].Longitude!.Value,
                    loadByRestaurant[id]))
                .ToList(),
            fleet
                .Where(vehicle => vehicle.IsAvailable)
                .Select(vehicle => new SweepVehicleCapacity(vehicle.CapacityKg, maxStops))
                .ToList());

        if (plan.Unassignable.Count > 0)
        {
            return Result<IReadOnlyList<RouteDto>>.Failure(Error.Validation(
                "FLEET_CAPACITY_EXCEEDED",
                $"Fleet cannot carry restaurants: {string.Join(", ", plan.Unassignable.Select(x => x.RestaurantId))}."));
        }

        var created = new List<RouteDto>(plan.Clusters.Count);
        foreach (var cluster in plan.Clusters)
        {
            var stops = new List<RouteStop>(cluster.Restaurants.Count + 1)
            {
                new(0, StopEntityType.hub, hub.Id, hub.Name, hub.Latitude.Value, hub.Longitude.Value, null, null)
            };
            stops.AddRange(cluster.Restaurants.Select((restaurant, index) =>
            {
                var coordinate = coordinates[restaurant.RestaurantId];
                return new RouteStop(
                    index + 1,
                    StopEntityType.restaurant,
                    restaurant.RestaurantId,
                    coordinate.Name,
                    restaurant.Latitude,
                    restaurant.Longitude,
                    null,
                    null);
            }));

            var optimization = optimizer.Optimize(stops, request.ServiceDate, criteria);
            var route = DeliveryRoute.CreateHubRoute(request.HubId, request.ServiceDate, stops, createdBy: null);
            route.ApplyOptimization(
                optimization.Stops,
                optimization.TotalDistanceKm,
                optimization.EstimatedDurationMinutes,
                optimization.EstimatedCost,
                criteria);
            await routes.AddAsync(route, ct);
            created.Add(route.ToDto());
        }

        await routes.SaveChangesAsync(ct);
        return Result<IReadOnlyList<RouteDto>>.Success(created);
    }
}
