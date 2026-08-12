using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketCodeReader(AppDbContext db) : IMarketCodeReader
{
    public async Task<IReadOnlyDictionary<Guid, (string? Code, string Name)>> ReadMarketCodesAsync(
        IReadOnlyCollection<Guid> marketIds,
        CancellationToken ct)
    {
        var ids = marketIds.Distinct().ToArray();
        return await db.Set<MarketCodeRow>()
            .AsNoTracking()
            .Where(row => ids.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => (row.Code, row.Name), ct);
    }
}
