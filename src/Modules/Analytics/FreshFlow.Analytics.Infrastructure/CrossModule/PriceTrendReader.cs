using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class PriceTrendReader(AppDbContext db) : IPriceTrendReader
{
    public async Task<PriceTrendReadModel> ReadAsync(
        IReadOnlyList<Guid> marketProductIds,
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct)
    {
        var ids = marketProductIds.ToArray();
        var hourlyBuckets = await db.Set<PriceSnapshotRow>()
            .AsNoTracking()
            .Where(row =>
                ids.Contains(row.MarketProductId) &&
                row.BucketStartUtc >= startUtcInclusive &&
                row.BucketStartUtc < endUtcExclusive)
            .OrderBy(row => row.MarketProductId)
            .ThenBy(row => row.BucketStartUtc)
            .Select(row => new PriceTrendHourlyBucketReadModel(
                row.MarketProductId,
                row.BucketStartUtc,
                row.MinPrice,
                row.MaxPrice,
                row.AvgPrice,
                row.SnapshotCount,
                row.PriceVolatility))
            .ToListAsync(ct);

        var marketProducts = await db.Set<MarketProductDetailRow>()
            .AsNoTracking()
            .Where(row => ids.Contains(row.MarketProductId))
            .Select(row => new MarketProductDetailReadModel(
                row.MarketProductId,
                row.ProductName,
                row.MarketName))
            .ToListAsync(ct);

        return new PriceTrendReadModel(hourlyBuckets, marketProducts);
    }
}
