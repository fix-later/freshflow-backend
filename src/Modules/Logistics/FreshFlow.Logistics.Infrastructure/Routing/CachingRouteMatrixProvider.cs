using System.Globalization;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Infrastructure.Persistence;
using FreshFlow.Logistics.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Logistics.Infrastructure.Routing;

internal sealed class CachingRouteMatrixProvider(
    IRouteMatrixProvider inner,
    IRouteMatrixCacheStore store,
    IVehicleCapacityPolicy settings,
    ILogger<CachingRouteMatrixProvider> logger) : IRouteMatrixProvider
{
    public async Task<RouteMatrixResult> GetMatrixAsync(
        IReadOnlyList<RoutePoint> points,
        IReadOnlyCollection<string> vehicleProfiles,
        CancellationToken cancellationToken)
    {
        if (!settings.MatrixCacheEnabled)
            return await inner.GetMatrixAsync(points, vehicleProfiles, cancellationToken);

        var profiles = vehicleProfiles.Distinct(StringComparer.Ordinal).Order().ToList();
        if (profiles.Count == 0)
            profiles.Add("car");

        var needed = BuildPairs(points, profiles);
        var keys = needed.Select(pair => pair.Key).Distinct(StringComparer.Ordinal).ToArray();
        var minCalculatedAt = DateTime.UtcNow.AddDays(-settings.MatrixCacheMaxAgeDays);
        var hits = keys.Length == 0
            ? new Dictionary<string, (long DistanceMeters, long DurationSeconds)>(StringComparer.Ordinal)
            : await store.GetAsync(keys, minCalculatedAt, cancellationToken);

        if (hits.Count == keys.Length)
        {
            logger.LogInformation(
                "Route matrix cache hit Points={PointCount} Profiles={ProfileCount}",
                points.Count,
                profiles.Count);
            return FromCache(points.Count, profiles, needed, hits);
        }

        // ponytail: coarse cache — a single new pair triggers a full-matrix refetch. Sparse fetch is
        // the upgrade if coordinate churn makes this path common; it requires changing Goong batching.
        var result = await inner.GetMatrixAsync(points, vehicleProfiles, cancellationToken);
        if (result.Provider == "GOONG")
            await store.UpsertAsync(BuildRows(points, result), cancellationToken);

        return result;
    }

    private static List<(string Key, string Profile, int From, int To)> BuildPairs(
        IReadOnlyList<RoutePoint> points,
        IReadOnlyList<string> profiles)
    {
        var pairs = new List<(string Key, string Profile, int From, int To)>();
        foreach (var profile in profiles)
            for (var from = 0; from < points.Count; from++)
                for (var to = 0; to < points.Count; to++)
                    if (from != to)
                        pairs.Add((Key(profile, points[from], points[to]), profile, from, to));
        return pairs;
    }

    private static RouteMatrixResult FromCache(
        int pointCount,
        IReadOnlyList<string> profiles,
        IReadOnlyList<(string Key, string Profile, int From, int To)> needed,
        IReadOnlyDictionary<string, (long DistanceMeters, long DurationSeconds)> hits)
    {
        var matrices = profiles.ToDictionary(
            profile => profile,
            _ => new ProfileRouteMatrix(Empty(pointCount), Empty(pointCount)),
            StringComparer.Ordinal);
        foreach (var pair in needed)
        {
            var value = hits[pair.Key];
            matrices[pair.Profile].DistanceMeters[pair.From][pair.To] = value.DistanceMeters;
            matrices[pair.Profile].DurationSeconds[pair.From][pair.To] = value.DurationSeconds;
        }
        return new RouteMatrixResult(matrices, "GOONG_CACHED", false, []);
    }

    private static IEnumerable<RouteMatrixCacheEntry> BuildRows(
        IReadOnlyList<RoutePoint> points,
        RouteMatrixResult result)
    {
        var calculatedAt = DateTime.UtcNow;
        foreach (var profile in result.Profiles)
            for (var from = 0; from < points.Count; from++)
                for (var to = 0; to < points.Count; to++)
                    if (from != to)
                        yield return new RouteMatrixCacheEntry(
                            Key(profile.Key, points[from], points[to]),
                            profile.Key,
                            points[from].Latitude,
                            points[from].Longitude,
                            points[to].Latitude,
                            points[to].Longitude,
                            profile.Value.DistanceMeters[from][to],
                            profile.Value.DurationSeconds[from][to],
                            calculatedAt);
    }

    private static string Key(string profile, RoutePoint from, RoutePoint to) => string.Join(':',
        profile,
        Coordinate(from.Latitude),
        Coordinate(from.Longitude),
        Coordinate(to.Latitude),
        Coordinate(to.Longitude));

    private static string Coordinate(decimal value) =>
        value.ToString("F7", CultureInfo.InvariantCulture);

    private static long[][] Empty(int count) =>
        Enumerable.Range(0, count).Select(_ => new long[count]).ToArray();
}
