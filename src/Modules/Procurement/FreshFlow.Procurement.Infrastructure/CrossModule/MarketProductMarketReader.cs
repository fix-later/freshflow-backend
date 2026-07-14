using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketProductMarketReader(AppDbContext db) : IMarketProductMarketReader
{
    public async Task<IReadOnlyDictionary<Guid, Guid>> ReadMarketsAsync(
        IReadOnlyCollection<Guid> marketProductIds,
        CancellationToken ct)
    {
        var ids = marketProductIds.Distinct().ToArray();

        return await db.Set<MarketProductMarketRow>()
            .AsNoTracking()
            .Where(row => ids.Contains(row.Id) && row.DeletedAt == null)
            .ToDictionaryAsync(row => row.Id, row => row.MarketId, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> ReadReferencePricesAsync(
        IReadOnlyCollection<Guid> marketProductIds,
        CancellationToken ct)
    {
        var ids = marketProductIds.Distinct().ToArray();

        return await db.Set<MarketProductMarketRow>()
            .AsNoTracking()
            .Where(row => ids.Contains(row.Id) && row.DeletedAt == null)
            .ToDictionaryAsync(row => row.Id, row => row.CurrentPrice, ct);
    }
}
