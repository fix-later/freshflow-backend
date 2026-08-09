namespace FreshFlow.Orders.Application.Abstractions;

public interface IRoadDistanceProvider
{
    public Task<RoadDistanceResult> GetDistanceAsync(
        IReadOnlyList<GeoCoordinate> origins,
        GeoCoordinate destination,
        CancellationToken cancellationToken);
}

public sealed record GeoCoordinate(decimal Latitude, decimal Longitude);

public sealed record RoadDistanceResult(
    int DistanceMeters,
    int DurationSeconds,
    GeoCoordinate ChosenOrigin,
    bool IsEstimated,
    string Provider,
    string? InputRevision = null);
