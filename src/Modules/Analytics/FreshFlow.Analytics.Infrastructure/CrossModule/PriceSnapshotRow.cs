namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class PriceSnapshotRow
{
    public Guid MarketProductId { get; init; }
    public DateTime BucketStartUtc { get; init; }
    public decimal MinPrice { get; init; }
    public decimal MaxPrice { get; init; }
    public decimal AvgPrice { get; init; }
    public int SnapshotCount { get; init; }
    public decimal? PriceVolatility { get; init; }
}
