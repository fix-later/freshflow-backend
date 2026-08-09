using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace FreshFlow.Logistics.Infrastructure.Optimization;

internal sealed class RoadRouteOptimizer(
    IRouteMatrixProvider matrices,
    IVehicleCapacityPolicy settings,
    IConfiguration config) : IRouteOptimizer
{
    private readonly NearestNeighborTwoOptOptimizer _legacy = new(config);

    public RouteOptimizationResult Optimize(
        IReadOnlyList<RouteStop> stops, DateOnly serviceDate, OptimizationCriteria criteria) =>
        _legacy.Optimize(stops, serviceDate, criteria);

    public RouteOptimizationResult Recalculate(IReadOnlyList<RouteStop> stops, DateOnly serviceDate) =>
        _legacy.Recalculate(stops, serviceDate);

    public async Task<RouteOptimizationResult> OptimizeAsync(
        IReadOnlyList<RouteStop> stops, DateOnly serviceDate, OptimizationCriteria criteria,
        string routingProfile, CancellationToken ct)
    {
        var matrix = await Matrix(stops, routingProfile, ct);
        var profile = matrix.Profiles[routingProfile];
        var pickups = stops.Select((stop, index) => (stop, index))
            .Where(x => x.stop.EntityType is StopEntityType.hub or StopEntityType.market).ToList();
        var remaining = stops.Select((stop, index) => (stop, index))
            .Where(x => x.stop.EntityType == StopEntityType.restaurant).ToList();
        var ordered = pickups.ToList();
        var current = pickups[^1].index;
        while (remaining.Count > 0)
        {
            var next = remaining.OrderBy(x => Arc(profile, current, x.index, criteria))
                .ThenBy(x => x.index).First();
            ordered.Add(next);
            remaining.Remove(next);
            current = next.index;
        }
        return Build(ordered, profile, serviceDate);
    }

    public async Task<RouteOptimizationResult> RecalculateAsync(
        IReadOnlyList<RouteStop> stops, DateOnly serviceDate, string routingProfile, CancellationToken ct)
    {
        var matrix = await Matrix(stops, routingProfile, ct);
        return Build(stops.Select((stop, index) => (stop, index)).ToList(),
            matrix.Profiles[routingProfile], serviceDate);
    }

    private async Task<RouteMatrixResult> Matrix(
        IReadOnlyList<RouteStop> stops, string profile, CancellationToken ct) =>
        await matrices.GetMatrixAsync(
            stops.Select(x => new RoutePoint(x.Latitude, x.Longitude)).ToList(), [profile], ct);

    private RouteOptimizationResult Build(
        IReadOnlyList<(RouteStop stop, int index)> order,
        ProfileRouteMatrix matrix,
        DateOnly serviceDate)
    {
        long distance = 0;
        long roadSeconds = 0;
        var current = TimeZoneInfo.ConvertTimeToUtc(
            serviceDate.ToDateTime(new TimeOnly(settings.StartHour, 0)),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
        var result = new List<RouteStop>(order.Count);
        for (var i = 0; i < order.Count; i++)
        {
            if (i > 0)
            {
                distance += matrix.DistanceMeters[order[i - 1].index][order[i].index];
                roadSeconds += matrix.DurationSeconds[order[i - 1].index][order[i].index];
                current = current.AddSeconds(matrix.DurationSeconds[order[i - 1].index][order[i].index]);
            }
            var departure = order[i].stop.EntityType == StopEntityType.restaurant
                ? current.AddMinutes(settings.ServiceTimeMinutes) : current;
            result.Add(order[i].stop with
            {
                StopOrder = i,
                EstimatedArrivalAt = current,
                EstimatedDepartureAt = departure
            });
            current = departure;
        }
        if (order.Count > 1)
        {
            distance += matrix.DistanceMeters[order[^1].index][order[0].index];
            roadSeconds += matrix.DurationSeconds[order[^1].index][order[0].index];
        }
        var duration = checked((int)Math.Ceiling(
            (roadSeconds + order.Count(x => x.stop.EntityType == StopEntityType.restaurant)
                * settings.ServiceTimeMinutes * 60d) / 60d));
        var distanceKm = Math.Round(distance / 1000m, 2);
        return new RouteOptimizationResult(
            result, distanceKm, duration, Math.Round(distanceKm * settings.CostPerKm, 2));
    }

    private static long Arc(ProfileRouteMatrix matrix, int from, int to, OptimizationCriteria criteria) =>
        criteria == OptimizationCriteria.time
            ? matrix.DurationSeconds[from][to]
            : matrix.DistanceMeters[from][to];
}
