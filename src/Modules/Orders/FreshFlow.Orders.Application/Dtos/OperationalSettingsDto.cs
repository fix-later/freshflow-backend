namespace FreshFlow.Orders.Application.Dtos;

public sealed record OperationalSettingsDto(
    TimeOnly DailyCutoffTime,
    bool BatchingEnabled,
    string DefaultRouteType,
    int DeliveryWindowDays,
    decimal DeliveryFeePerKm,
    DateTime UpdatedAt,
    decimal BaseFee = 0m,
    decimal MinimumFee = 0m,
    decimal RoundingUnit = 0m);
