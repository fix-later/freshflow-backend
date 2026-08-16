namespace FreshFlow.Analytics.Application.Abstractions;

public interface IDeliveryPerformanceReader
{
    public Task<DeliveryPerformanceReadModel> ReadAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct);
}

public sealed record DeliveryPerformanceReadModel(
    int TotalDeliveries,
    int OnTimeCount,
    int LateCount,
    int FailedCount,
    decimal? AvgDeliveryDurationMinutes,
    int DurationSampleCount,
    IReadOnlyList<decimal> VehicleUtilizationPercentages);
