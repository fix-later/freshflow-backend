namespace FreshFlow.Analytics.Application.Dtos;

public sealed record OrderMetricsDto(
    OrderMetricsSummaryDto Summary,
    IReadOnlyList<OrderMetricsBucketDto> Buckets);

public sealed record OrderMetricsSummaryDto(
    int TotalOrders,
    decimal TotalRevenueVND,
    decimal AvgOrderValueVND,
    int CancelledCount,
    decimal CancellationRatePercent,
    int DeliveredCount,
    IReadOnlyDictionary<string, int> StatusCounts);

public sealed record OrderMetricsBucketDto(
    DateOnly Date,
    int OrderCount,
    decimal RevenueVND);
