using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Common;

public sealed class RoutePlanningInputBuilder(
    IHubCoordinateReader hubs,
    IRestaurantCoordinateReader restaurants,
    IOrderStatusReader orders,
    IOrderPackingReader packing,
    IDeliveryRepository deliveries,
    IDeliveryRouteRepository routes,
    IVehicleRepository vehicles,
    IMarketSessionVehicleReader sessionVehicles,
    IVehicleCapacityPolicy settings) : IRoutePlanningInputBuilder
{
    private static readonly string[] RoutableOrderStatuses = ["Batched", "PickedUp", "AtHub"];

    public async Task<Result<RoutePlanningInput>> BuildAsync(
        Guid hubId, DateOnly serviceDate, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(hubId, ct);
        if (hub is null)
            return Result<RoutePlanningInput>.Failure(Error.NotFound("HUB", hubId));
        if (!Valid(hub.Latitude, hub.Longitude))
            return MissingCoordinates($"Hub '{hubId}' has no coordinates configured.");

        var routable = await orders.ListRoutableRestaurantsAsync(serviceDate, RoutableOrderStatuses, ct);
        var routableOrders = await orders.ListByRestaurantsAndStatusAsync(
            routable.Select(x => x.RestaurantId).ToList(), RoutableOrderStatuses, ct, hubId, serviceDate);
        var reservedOrderIds = await deliveries.GetExistingOrderIdsAsync(
            routableOrders.Select(x => x.OrderId).ToList(), ct);
        routableOrders = routableOrders.Where(x => !reservedOrderIds.Contains(x.OrderId)).ToList();
        if (routableOrders.Count == 0)
        {
            return Result<RoutePlanningInput>.Success(new RoutePlanningInput(
                hubId, hub.Name, hub.Latitude.Value, hub.Longitude.Value, serviceDate,
                [], [], Revision("empty", hubId, serviceDate)));
        }

        // Stops are located at the address captured at checkout (orders.delivery_latitude/longitude),
        // not the restaurant's default address. The restaurant record only supplies the display name.
        // ponytail: multiple checkout addresses for one restaurant in a session collapse to a single stop
        // (the earliest order's coords); split into per-address stops if chains order to several branches/day.
        var badOrder = routableOrders.FirstOrDefault(o => !Valid(o.DeliveryLatitude, o.DeliveryLongitude));
        if (badOrder is not null)
            return MissingCoordinates($"Order '{badOrder.OrderId}' has no checkout delivery coordinates.");

        var nameByRestaurant = new Dictionary<Guid, string>();
        foreach (var restaurantId in routableOrders.Select(x => x.RestaurantId).Distinct().Order())
        {
            var restaurant = await restaurants.FindByRestaurantIdAsync(restaurantId, ct);
            if (restaurant is null)
                return MissingCoordinates($"Restaurant '{restaurantId}' has no coordinates configured.");
            nameByRestaurant[restaurantId] = restaurant.Name;
        }

        var packingRows = await packing.GetLinesByOrdersAsync(routableOrders.Select(x => x.OrderId).ToList(), ct);
        var packingByOrder = packingRows.ToDictionary(x => x.OrderId, x => x.Lines);
        if (routableOrders.Any(order => !packingByOrder.TryGetValue(order.OrderId, out var lines)
                || lines.Count == 0 || lines.Any(line => line.CapacityKg is null or <= 0m)))
        {
            return Result<RoutePlanningInput>.Failure(Error.Validation(
                "ROUTE_WEIGHT_INCOMPLETE",
                "Every routable order line must have a valid packing code before route planning."));
        }

        decimal LineLoad(OrderPackingLine line)
        {
            var capacity = line.CapacityKg!.Value;
            return line.Quantity + Math.Ceiling(line.Quantity / capacity) * settings.BoxTareKg;
        }

        var demands = routableOrders
            .GroupBy(order => order.RestaurantId)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var anchor = group.OrderBy(x => x.OrderId).First();
                var orderIds = group.Select(x => x.OrderId).Order().ToList().AsReadOnly();
                return new RestaurantDemand(
                    group.Key, nameByRestaurant[group.Key], orderIds,
                    anchor.DeliveryLatitude!.Value, anchor.DeliveryLongitude!.Value,
                    orderIds.Sum(orderId => packingByOrder[orderId].Sum(LineLoad)));
            })
            .ToList()
            .AsReadOnly();

        var (fleet, _) = await vehicles.GetPageAsync(null, 10_000, true, hubId, ct);
        var reservedVehicleIds = await routes.GetReservedVehicleIdsAsync(serviceDate, ct);
        var assignedVehicleIds = await sessionVehicles.ReadAssignedVehicleIdsAsync(hubId, serviceDate, ct);
        var planningVehicles = fleet
            .Where(vehicle =>
                vehicle.IsAvailable &&
                !reservedVehicleIds.Contains(vehicle.Id) &&
                (assignedVehicleIds.Count == 0 || assignedVehicleIds.Contains(vehicle.Id)))
            .OrderBy(vehicle => vehicle.Id)
            .Select(vehicle => new PlanningVehicle(
                vehicle.Id, vehicle.PlateNumber, vehicle.VehicleType, Profile(vehicle.VehicleType),
                vehicle.CapacityKg,
                vehicle.CapacityKg * settings.CapacityUtilizationPercent / 100m))
            .ToList()
            .AsReadOnly();

        var snapshot = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"{hubId:N}|{serviceDate:yyyy-MM-dd}|{hub.Latitude}|{hub.Longitude}|")
            .Append(CultureInfo.InvariantCulture,
                $"{settings.CapacityUtilizationPercent}|{settings.MaxStopsPerVehicle}|{settings.BoxTareKg}|{settings.MatrixBatchSize}|{settings.SolverTimeLimitSeconds}|{settings.StartHour}|{settings.ServiceTimeMinutes}|{settings.CostPerKm}|");
        foreach (var demand in demands)
        {
            snapshot.Append(CultureInfo.InvariantCulture,
                $"R:{demand.RestaurantId:N}:{demand.Latitude}:{demand.Longitude}:{demand.LoadKg}:");
            foreach (var orderId in demand.OrderIds)
            {
                snapshot.Append($"O:{orderId:N}:");
                foreach (var line in packingByOrder[orderId].OrderBy(x => x.OrderItemId))
                    snapshot.Append(CultureInfo.InvariantCulture, $"{line.OrderItemId:N}:{line.Quantity}:{line.CapacityKg};");
            }
        }
        foreach (var vehicle in fleet
                     .Where(vehicle => assignedVehicleIds.Count == 0 || assignedVehicleIds.Contains(vehicle.Id))
                     .OrderBy(x => x.Id))
            snapshot.Append(CultureInfo.InvariantCulture,
                $"V:{vehicle.Id:N}:{vehicle.CapacityKg}:{vehicle.VehicleType}:{vehicle.IsAvailable}:{vehicle.DeletedAt is null}:{reservedVehicleIds.Contains(vehicle.Id)};");

        return Result<RoutePlanningInput>.Success(new RoutePlanningInput(
            hubId, hub.Name, hub.Latitude.Value, hub.Longitude.Value, serviceDate,
            demands, planningVehicles, Revision(snapshot.ToString(), hubId, serviceDate)));
    }

    internal static string Profile(VehicleType type) => type switch
    {
        VehicleType.truck => "truck",
        VehicleType.motorbike => "bike",
        _ => "car"
    };

    private static Result<RoutePlanningInput> MissingCoordinates(string message) =>
        Result<RoutePlanningInput>.Failure(Error.Validation("MISSING_COORDINATES", message));

    private static bool Valid(decimal? latitude, decimal? longitude) =>
        latitude is >= -90m and <= 90m && longitude is >= -180m and <= 180m;

    private static string Revision(string snapshot, Guid hubId, DateOnly date)
    {
        var bytes = Encoding.UTF8.GetBytes($"{hubId:N}|{date:yyyy-MM-dd}|{snapshot}");
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
