namespace FreshFlow.Logistics.Application.Dtos;

public sealed record DriverRouteDto(
    Guid RouteId,
    DateOnly ServiceDate,
    string Status,
    IReadOnlyList<RouteStopDto> Stops,
    IReadOnlyList<DriverDeliveryDto> Deliveries);
