namespace FreshFlow.Analytics.Application.Dtos;

public sealed record ProcurementMetricsDto(
    int TotalBatches,
    IReadOnlyDictionary<string, int> StatusCounts,
    decimal CompletionRatePercent,
    int ItemsTotal,
    int ItemsPurchased,
    int ItemsPending,
    decimal TotalActualCostVND,
    decimal? PriceVariancePercent,
    decimal? AvgLeadTimeMinutes,
    int ExceptionCount,
    IReadOnlyDictionary<string, int> ExceptionsByType);
