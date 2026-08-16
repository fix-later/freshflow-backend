namespace FreshFlow.Analytics.Application.Abstractions;

public interface IPriceTrendReader
{
    public Task<PriceTrendReadModel> ReadAsync(
        IReadOnlyList<Guid> marketProductIds,
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct);
}

public sealed record PriceTrendReadModel(
    IReadOnlyList<PriceTrendHourlyBucketReadModel> HourlyBuckets,
    IReadOnlyList<MarketProductDetailReadModel> MarketProducts);

public sealed record PriceTrendHourlyBucketReadModel(
    Guid MarketProductId,
    DateTime BucketStartUtc,
    decimal MinPrice,
    decimal MaxPrice,
    decimal AvgPrice,
    int SnapshotCount,
    decimal? PriceVolatility);

public sealed record MarketProductDetailReadModel(
    Guid MarketProductId,
    string ProductName,
    string MarketName);
