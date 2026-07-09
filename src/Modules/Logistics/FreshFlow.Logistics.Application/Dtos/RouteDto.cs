namespace FreshFlow.Logistics.Application.Dtos;

public sealed record RouteDto(
    Guid Id,
    string RouteType,
    string Status,
    DateOnly ServiceDate,
    IReadOnlyList<RouteStopDto> Stops,
    decimal? TotalDistanceKm,
    int? EstimatedDurationMinutes,
    decimal? EstimatedCost,
    Guid? VehicleId,
    Guid? OrderGroupId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
