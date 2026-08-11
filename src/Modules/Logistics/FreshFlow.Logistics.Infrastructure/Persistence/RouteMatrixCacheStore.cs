using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.Persistence;

internal sealed class RouteMatrixCacheStore(AppDbContext db) : IRouteMatrixCacheStore
{
    public async Task<IReadOnlyDictionary<string, (long DistanceMeters, long DurationSeconds)>> GetAsync(
        IEnumerable<string> keys,
        DateTime minCalculatedAt,
        CancellationToken ct)
    {
        var pairKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
        return await db.Set<RouteMatrixCacheEntry>()
            .AsNoTracking()
            .Where(row => pairKeys.Contains(row.PairKey) && row.CalculatedAt >= minCalculatedAt)
            .ToDictionaryAsync(
                row => row.PairKey,
                row => ValueTuple.Create(row.DistanceMeters, row.DurationSeconds),
                StringComparer.Ordinal,
                ct);
    }

    public async Task UpsertAsync(IEnumerable<RouteMatrixCacheEntry> rows, CancellationToken ct)
    {
        var incoming = rows
            .GroupBy(row => row.PairKey, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToDictionary(row => row.PairKey, StringComparer.Ordinal);
        var keys = incoming.Keys.ToArray();
        var existing = await db.Set<RouteMatrixCacheEntry>()
            .Where(row => keys.Contains(row.PairKey))
            .ToDictionaryAsync(row => row.PairKey, StringComparer.Ordinal, ct);

        foreach (var row in incoming.Values)
        {
            if (existing.TryGetValue(row.PairKey, out var current))
                current.UpdateFrom(row);
            else
                await db.Set<RouteMatrixCacheEntry>().AddAsync(row, ct);
        }

        await db.SaveChangesAsync(ct);
    }
}
