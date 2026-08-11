namespace FreshFlow.Logistics.Infrastructure.Persistence.Entities;

internal sealed class RouteMatrixCacheEntry
{
    private RouteMatrixCacheEntry() { }

    public RouteMatrixCacheEntry(
        string pairKey,
        string profile,
        decimal fromLatitude,
        decimal fromLongitude,
        decimal toLatitude,
        decimal toLongitude,
        long distanceMeters,
        long durationSeconds,
        DateTime calculatedAt)
    {
        PairKey = pairKey;
        Profile = profile;
        FromLatitude = fromLatitude;
        FromLongitude = fromLongitude;
        ToLatitude = toLatitude;
        ToLongitude = toLongitude;
        DistanceMeters = distanceMeters;
        DurationSeconds = durationSeconds;
        CalculatedAt = calculatedAt;
    }

    public string PairKey { get; private set; } = string.Empty;
    public string Profile { get; private set; } = string.Empty;
    public decimal FromLatitude { get; private set; }
    public decimal FromLongitude { get; private set; }
    public decimal ToLatitude { get; private set; }
    public decimal ToLongitude { get; private set; }
    public long DistanceMeters { get; private set; }
    public long DurationSeconds { get; private set; }
    public DateTime CalculatedAt { get; private set; }

    public void UpdateFrom(RouteMatrixCacheEntry row)
    {
        Profile = row.Profile;
        FromLatitude = row.FromLatitude;
        FromLongitude = row.FromLongitude;
        ToLatitude = row.ToLatitude;
        ToLongitude = row.ToLongitude;
        DistanceMeters = row.DistanceMeters;
        DurationSeconds = row.DurationSeconds;
        CalculatedAt = row.CalculatedAt;
    }
}
