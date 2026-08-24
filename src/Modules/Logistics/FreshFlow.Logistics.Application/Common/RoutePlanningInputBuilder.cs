using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
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
                [], [], Revision("empty", hubId, serviceDate), []));
        }

        // Stops are located at the address captured at checkout (orders.delivery_latitude/longitude),
        // not the restaurant's default address. The restaurant record only supplies the display name.
        // ponytail: multiple checkout addresses for one restaurant in a session collapse to a single stop
        // (the earliest order's coords); split into per-address stops if chains order to several branches/day.

        // M7: an order with incomplete data (coords / restaurant row / packing) is excluded from
        // planning instead of failing the whole hub's day — see BR-LOG-015. A missing restaurant
        // row excludes every order for that restaurant; coords/packing are checked per order.
        var nameByRestaurant = new Dictionary<Guid, string>();
        var missingRestaurantIds = new HashSet<Guid>();
        foreach (var restaurantId in routableOrders.Select(x => x.RestaurantId).Distinct().Order())
        {
            var restaurant = await restaurants.FindByRestaurantIdAsync(restaurantId, ct);
            if (restaurant is null)
            {
                missingRestaurantIds.Add(restaurantId);
                nameByRestaurant[restaurantId] = $"Restaurant {restaurantId}";
            }
            else
            {
                nameByRestaurant[restaurantId] = restaurant.Name;
            }
        }

        var packingRows = await packing.GetLinesByOrdersAsync(routableOrders.Select(x => x.OrderId).ToList(), ct);
        var packingByOrder = packingRows.ToDictionary(x => x.OrderId, x => x.Lines);

        bool HasValidPacking(Guid orderId) =>
            packingByOrder.TryGetValue(orderId, out var lines) && lines.Count > 0
                && lines.All(line => line.CapacityKg is > 0m);

        string? ExcludeReason(OrderStatusLookupDto order)
        {
            if (missingRestaurantIds.Contains(order.RestaurantId))
                return "Restaurant record is unavailable.";
            if (!Valid(order.DeliveryLatitude, order.DeliveryLongitude))
                return "Order has no checkout delivery coordinates.";
            if (!HasValidPacking(order.OrderId))
                return "One or more order lines have no valid packing code.";
            return null;
        }

        var classified = routableOrders.Select(order => (Order: order, Reason: ExcludeReason(order))).ToList();
        var includedOrders = classified.Where(x => x.Reason is null).Select(x => x.Order).ToList();
        var excludedOrders = classified.Where(x => x.Reason is not null).ToList();

        decimal LineLoad(OrderPackingLine line)
        {
            var capacity = line.CapacityKg!.Value;
            return line.Quantity + Math.Ceiling(line.Quantity / capacity) * settings.BoxTareKg;
        }

        var demands = includedOrders
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

        var excluded = excludedOrders
            .GroupBy(x => x.Order.RestaurantId)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var orderIds = group.Select(x => x.Order.OrderId).Order().ToList().AsReadOnly();
                var reason = string.Join("; ", group.Select(x => x.Reason!).Distinct());
                return new RoutePlanUnassigned(
                    group.Key, nameByRestaurant[group.Key], orderIds, 0m, reason,
                    ExcludedForIncompleteData: true);
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
        // M7: excluded order ids must shift the revision hash — otherwise a plan built while an
        // order was excluded stays "fresh" once that order's data is fixed, and it never re-enters
        // planning (input.Excluded is only recomputed by re-running BuildAsync).
        foreach (var orderId in excludedOrders.Select(x => x.Order.OrderId).Order())
            snapshot.Append(CultureInfo.InvariantCulture, $"X:{orderId:N};");
        foreach (var vehicle in fleet
                     .Where(vehicle => assignedVehicleIds.Count == 0 || assignedVehicleIds.Contains(vehicle.Id))
                     .OrderBy(x => x.Id))
            snapshot.Append(CultureInfo.InvariantCulture,
                $"V:{vehicle.Id:N}:{vehicle.CapacityKg}:{vehicle.VehicleType}:{vehicle.IsAvailable}:{vehicle.DeletedAt is null}:{reservedVehicleIds.Contains(vehicle.Id)};");

        return Result<RoutePlanningInput>.Success(new RoutePlanningInput(
            hubId, hub.Name, hub.Latitude.Value, hub.Longitude.Value, serviceDate,
            demands, planningVehicles, Revision(snapshot.ToString(), hubId, serviceDate), excluded));
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
