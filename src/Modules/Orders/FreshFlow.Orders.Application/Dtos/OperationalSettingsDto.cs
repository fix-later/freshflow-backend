namespace FreshFlow.Orders.Application.Dtos;

public sealed record OperationalSettingsDto(
    TimeOnly DailyCutoffTime,
    bool BatchingEnabled,
    string DefaultRouteType,
    int DeliveryWindowDays,
    decimal DeliveryFeePerKm,
    DateTime UpdatedAt);
