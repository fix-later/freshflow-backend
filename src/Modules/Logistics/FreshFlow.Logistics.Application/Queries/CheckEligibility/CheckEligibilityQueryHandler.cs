using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.CheckEligibility;

internal sealed class CheckEligibilityQueryHandler(
    IDeliveryRouteRepository routes,
    IVehicleRepository vehicles,
    IDriverReader drivers,
    IVehicleCapacityPolicy capacityPolicy,
    IOrderStatusReader orders,
    IOrderPackingReader packing)
    : IRequestHandler<CheckEligibilityQuery, Result<EligibilityResultDto>>
{
    public async Task<Result<EligibilityResultDto>> Handle(CheckEligibilityQuery request, CancellationToken ct)
    {
        var route = await routes.FindByIdAsync(request.RouteId, ct);
        if (route is null)
            return Result<EligibilityResultDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.RouteId));
        var restaurantIds = route.Stops
            .Where(stop => stop.EntityType == StopEntityType.restaurant)
            .Select(stop => stop.EntityId)
            .ToList();
        var atHubOrders = await orders.ListByRestaurantsAndStatusAsync(
            restaurantIds, ["AtHub"], ct, route.HubId, route.ServiceDate);
        var packingByOrder = atHubOrders.Count == 0
            ? []
            : await packing.GetLinesByOrdersAsync(
                atHubOrders.Select(order => order.OrderId).ToList(), ct);
        var lines = packingByOrder.SelectMany(entry => entry.Lines).ToList();
        var isWeightComplete = route.PlannedLoadKg is not null || atHubOrders.Count == 0
            || (packingByOrder.Count == atHubOrders.Count
                && lines.Count > 0
                && lines.All(line => line.CapacityKg is > 0m));
        var routeLoadKg = route.PlannedLoadKg ?? lines.Sum(line =>
            line.CapacityKg is { } capacityKg && capacityKg > 0m
                ? line.Quantity + Math.Ceiling(line.Quantity / capacityKg) * capacityPolicy.BoxTareKg
                : 0m);


        var reasons = new List<string>();

        if (!isWeightComplete)
            reasons.Add("ROUTE_WEIGHT_INCOMPLETE");

        var vehicle = await vehicles.FindByIdAsync(request.VehicleId, ct);
        if (vehicle is null)
        {
            reasons.Add("VEHICLE_NOT_FOUND");
        }
        else
        {
            if (vehicle.DeletedAt is not null)
                reasons.Add("VEHICLE_INACTIVE");

            if (!vehicle.IsAvailable)
                reasons.Add("VEHICLE_UNAVAILABLE");

            if (route.Stops.Count > capacityPolicy.MaxStopsPerVehicle)
                reasons.Add("VEHICLE_CAPACITY_EXCEEDED");
            if (routeLoadKg > vehicle.CapacityKg * capacityPolicy.CapacityUtilizationPercent / 100m)
                reasons.Add("VEHICLE_WEIGHT_CAPACITY_EXCEEDED");


            if (await routes.ExistsOtherRouteForVehicleOnDateAsync(
                    request.VehicleId,
                    route.ServiceDate,
                    request.RouteId,
                    ct))
            {
                reasons.Add("VEHICLE_DOUBLE_BOOKED");
            }
        }

        var driver = request.DriverUserId is { } driverUserId
            ? await drivers.FindByUserIdAsync(driverUserId, ct)
            : null;
        if (request.DriverUserId is null)
        {
            reasons.Add("DRIVER_REQUIRED");
        }
        else if (driver is null)
        {
            reasons.Add("DRIVER_NOT_FOUND");
        }
        else
        {
            if (!string.Equals(driver.RoleName, "driver", StringComparison.OrdinalIgnoreCase))
                reasons.Add("DRIVER_NOT_DRIVER_ROLE");

            if (!driver.IsActive)
                reasons.Add("DRIVER_INACTIVE");
        }

        return Result<EligibilityResultDto>.Success(new EligibilityResultDto(
            reasons.Count == 0,
            reasons.AsReadOnly(),
            routeLoadKg,
            vehicle?.CapacityKg ?? 0m,
            isWeightComplete));
    }
}
