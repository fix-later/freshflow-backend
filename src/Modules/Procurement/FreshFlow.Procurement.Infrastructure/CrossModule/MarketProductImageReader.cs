using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketProductImageReader(AppDbContext db) : IMarketProductImageReader
{
    public async Task<IReadOnlyDictionary<Guid, string>> ReadImagesAsync(
        IReadOnlyCollection<Guid> marketProductIds,
        CancellationToken ct)
    {
        var ids = marketProductIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, string>();

        return await db.Set<MarketProductImageRow>()
            .AsNoTracking()
            .Where(row => ids.Contains(row.MarketProductId) && row.ImageUrl != null)
            .ToDictionaryAsync(row => row.MarketProductId, row => row.ImageUrl!, ct);
    }
}
