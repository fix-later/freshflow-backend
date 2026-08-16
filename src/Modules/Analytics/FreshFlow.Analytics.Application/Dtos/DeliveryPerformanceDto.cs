namespace FreshFlow.Analytics.Application.Dtos;

public sealed record DeliveryPerformanceDto(
    int TotalDeliveries,
    int OnTimeCount,
    int LateCount,
    decimal OnTimeRatePercent,
    int FailedCount,
    decimal? AvgDeliveryDurationMinutes,
    int DurationSampleCount,
    decimal? AvgVehicleUtilizationPercent,
    int UtilizationSampleCount);
