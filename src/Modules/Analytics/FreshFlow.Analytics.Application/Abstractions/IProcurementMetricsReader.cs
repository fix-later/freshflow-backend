namespace FreshFlow.Analytics.Application.Abstractions;

public interface IProcurementMetricsReader
{
    public Task<ProcurementMetricsReadModel> ReadAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        Guid? marketId,
        CancellationToken ct);
}

public sealed record ProcurementMetricsReadModel(
    IReadOnlyList<ProcurementNamedCountReadModel> StatusCounts,
    int ItemsTotal,
    int ItemsPurchased,
    decimal TotalActualCostVND,
    decimal? PriceVariancePercent,
    decimal? AvgLeadTimeMinutes,
    IReadOnlyList<ProcurementNamedCountReadModel> ExceptionsByType);

public sealed record ProcurementNamedCountReadModel(string Name, int Count);
