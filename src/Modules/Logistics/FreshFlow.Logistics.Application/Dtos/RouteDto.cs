namespace FreshFlow.Logistics.Application.Dtos;

public sealed record RouteDto(
    Guid Id,
    Guid? HubId,
    string RouteType,
    string Status,
    DateOnly ServiceDate,
    IReadOnlyList<RouteStopDto> Stops,
    decimal? TotalDistanceKm,
    int? EstimatedDurationMinutes,
    decimal? EstimatedCost,
    string? OptimizationCriteria,
    Guid? VehicleId,
    Guid? DriverUserId,
    Guid? OrderGroupId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
