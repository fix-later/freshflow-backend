namespace FreshFlow.Logistics.Application.Dtos;

public sealed record StartRouteResultDto(
    Guid RouteId,
    string Status,
    int StartedOrderCount);
