using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketProductImageReader(AppDbContext db) : IMarketProductImageReader
{
    public async Task<IReadOnlyDictionary<Guid, MarketProductInfoDto>> ReadInfoAsync(
        IReadOnlyCollection<Guid> marketProductIds,
        CancellationToken ct)
    {
        var ids = marketProductIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, MarketProductInfoDto>();

        return await db.Set<MarketProductImageRow>()
            .AsNoTracking()
            .Where(row => ids.Contains(row.MarketProductId))
            .ToDictionaryAsync(
                row => row.MarketProductId,
                row => new MarketProductInfoDto(
                    row.ImageUrl,
                    row.PackingCode,
                    row.PackingCapacityKg),
                ct);
    }
}
