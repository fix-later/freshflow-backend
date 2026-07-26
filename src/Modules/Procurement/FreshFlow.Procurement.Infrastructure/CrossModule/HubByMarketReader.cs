using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class HubByMarketReader(AppDbContext db) : IHubByMarketReader
{
    public Task<bool> IsActiveAsync(Guid hubId, CancellationToken ct) =>
        db.Set<HubByMarketRow>()
            .AsNoTracking()
            .AnyAsync(row => row.HubId == hubId, ct);

    public async Task<IReadOnlyDictionary<Guid, Guid>> ReadActiveHubsAsync(
        IReadOnlyCollection<Guid> marketIds,
        CancellationToken ct)
    {
        var ids = marketIds.Distinct().ToArray();
        return await db.Set<HubByMarketRow>()
            .AsNoTracking()
            .Where(row => ids.Contains(row.MarketId))
            .ToDictionaryAsync(row => row.MarketId, row => row.HubId, ct);
    }
}
