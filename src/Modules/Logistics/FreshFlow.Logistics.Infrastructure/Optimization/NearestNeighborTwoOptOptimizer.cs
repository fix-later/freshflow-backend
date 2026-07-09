using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace FreshFlow.Logistics.Infrastructure.Optimization;

internal sealed class NearestNeighborTwoOptOptimizer(IConfiguration config) : IRouteOptimizer
{
    private readonly double _avgSpeedKmh = ReadDouble(config, "Logistics:Optimization:AvgSpeedKmh", 30);
    private readonly decimal _costPerKm = ReadDecimal(config, "Logistics:Optimization:CostPerKm", 5000);
    private readonly int _serviceTimeMinutes = ReadInt(config, "Logistics:Optimization:ServiceTimeMinutes", 10);
    private readonly int _startHour = ReadHour(config, "Logistics:Optimization:StartHour", 6);

    public RouteOptimizationResult Optimize(
        IReadOnlyList<RouteStop> stops,
        DateOnly serviceDate,
        OptimizationCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(stops);

        var orderedStops = stops.OrderBy(stop => stop.StopOrder).ToList();
        var marketStops = orderedStops
            .Where(stop => stop.EntityType == StopEntityType.market)
            .ToList();
        var restaurantStops = orderedStops
            .Where(stop => stop.EntityType == StopEntityType.restaurant)
            .ToList();

        var optimizedRestaurants = restaurantStops.Count <= 1
            ? restaurantStops
            : ImproveWithTwoOpt(
                BuildNearestNeighborOrder(marketStops[^1], restaurantStops),
                marketStops[^1]);

        var optimizedStops = marketStops
            .Concat(optimizedRestaurants)
            .Select((stop, index) => stop with { StopOrder = index })
            .ToList();

        var totalDistanceKm = CalculateTotalDistance(optimizedStops);
        var roundedDistanceKm = Math.Round((decimal)totalDistanceKm, 2, MidpointRounding.AwayFromZero);
        var durationMinutes = (int)Math.Round(totalDistanceKm / _avgSpeedKmh * 60, MidpointRounding.AwayFromZero);
        var estimatedCost = Math.Round(roundedDistanceKm * _costPerKm, 2, MidpointRounding.AwayFromZero);

        // MVP caveat: avg_speed_kmh and cost_per_km are constants for every edge,
        // so TIME and COST are monotonic functions of DISTANCE. The selected criterion
        // is persisted/emphasized, but it does not change stop order until richer
        // road-distance and cost models are introduced.
        _ = criteria;

        return new RouteOptimizationResult(
            ApplyProvisionalEtas(optimizedStops, serviceDate).AsReadOnly(),
            roundedDistanceKm,
            durationMinutes,
            estimatedCost);
    }

    private List<RouteStop> BuildNearestNeighborOrder(
        RouteStop anchor,
        IReadOnlyList<RouteStop> restaurantStops)
    {
        var remaining = restaurantStops.ToList();
        var result = new List<RouteStop>(restaurantStops.Count);
        var current = anchor;

        while (remaining.Count > 0)
        {
            var next = remaining
                .OrderBy(stop => Distance(current, stop))
                .ThenBy(stop => stop.StopOrder)
                .First();

            result.Add(next);
            remaining.Remove(next);
            current = next;
        }

        return result;
    }

    private static List<RouteStop> ImproveWithTwoOpt(List<RouteStop> order, RouteStop anchor)
    {
        var improved = true;

        while (improved)
        {
            improved = false;

            for (var i = 0; i < order.Count - 1; i++)
            {
                for (var j = i + 1; j < order.Count; j++)
                {
                    var currentDistance = AnchoredRestaurantDistance(anchor, order);
                    var candidate = order.ToList();
                    candidate.Reverse(i, j - i + 1);
                    var candidateDistance = AnchoredRestaurantDistance(anchor, candidate);

                    if (candidateDistance >= currentDistance - 0.000001)
                        continue;

                    order = candidate;
                    improved = true;
                }
            }
        }

        return order;
    }

    private List<RouteStop> ApplyProvisionalEtas(IReadOnlyList<RouteStop> stops, DateOnly serviceDate)
    {
        var result = new List<RouteStop>(stops.Count);
        var currentDeparture = serviceDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(_startHour)));

        for (var i = 0; i < stops.Count; i++)
        {
            var arrival = i == 0
                ? currentDeparture
                : currentDeparture.AddMinutes(TravelMinutes(stops[i - 1], stops[i]));
            var departure = arrival.AddMinutes(_serviceTimeMinutes);

            result.Add(stops[i] with
            {
                EstimatedArrivalAt = arrival,
                EstimatedDepartureAt = departure
            });

            currentDeparture = departure;
        }

        return result;
    }

    private double TravelMinutes(RouteStop from, RouteStop to) =>
        Distance(from, to) / _avgSpeedKmh * 60;

    private static double CalculateTotalDistance(IReadOnlyList<RouteStop> stops)
    {
        var total = 0d;
        for (var i = 1; i < stops.Count; i++)
            total += Distance(stops[i - 1], stops[i]);

        return total;
    }

    private static double AnchoredRestaurantDistance(RouteStop anchor, IReadOnlyList<RouteStop> restaurants)
    {
        if (restaurants.Count == 0)
            return 0;

        var total = Distance(anchor, restaurants[0]);
        for (var i = 1; i < restaurants.Count; i++)
            total += Distance(restaurants[i - 1], restaurants[i]);

        return total;
    }

    private static double Distance(RouteStop from, RouteStop to) =>
        HaversineDistanceCalculator.DistanceKm(
            from.Latitude,
            from.Longitude,
            to.Latitude,
            to.Longitude);

    private static double ReadDouble(IConfiguration config, string key, double fallback) =>
        double.TryParse(config[key], out var value) && value > 0 ? value : fallback;

    private static decimal ReadDecimal(IConfiguration config, string key, decimal fallback) =>
        decimal.TryParse(config[key], out var value) && value >= 0 ? value : fallback;

    private static int ReadInt(IConfiguration config, string key, int fallback) =>
        int.TryParse(config[key], out var value) && value >= 0 ? value : fallback;

    private static int ReadHour(IConfiguration config, string key, int fallback) =>
        int.TryParse(config[key], out var value) && value >= 0 && value < 24 ? value : fallback;
}
