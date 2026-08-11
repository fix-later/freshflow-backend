using FreshFlow.Logistics.Infrastructure.Persistence.Entities;

namespace FreshFlow.Logistics.Infrastructure.Persistence;

internal interface IRouteMatrixCacheStore
{
    public Task<IReadOnlyDictionary<string, (long DistanceMeters, long DurationSeconds)>> GetAsync(
        IEnumerable<string> keys,
        DateTime minCalculatedAt,
        CancellationToken ct);

    public Task UpsertAsync(IEnumerable<RouteMatrixCacheEntry> rows, CancellationToken ct);
}
