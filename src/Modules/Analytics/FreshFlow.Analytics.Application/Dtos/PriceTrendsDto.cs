namespace FreshFlow.Analytics.Application.Dtos;

public sealed record PriceTrendsDto(IReadOnlyList<PriceTrendSeriesDto> Series);

public sealed record PriceTrendSeriesDto(
    Guid MarketProductId,
    string ProductName,
    string MarketName,
    string Interval,
    PriceTrendSummaryDto Summary,
    IReadOnlyList<PriceTrendPointDto> Points);

public sealed record PriceTrendSummaryDto(
    decimal MinPrice,
    decimal MaxPrice,
    decimal AvgPrice,
    decimal? PriceVolatility);

public sealed record PriceTrendPointDto(
    DateTimeOffset Timestamp,
    decimal AvgPrice,
    decimal MinPrice,
    decimal MaxPrice,
    int SnapshotCount);
