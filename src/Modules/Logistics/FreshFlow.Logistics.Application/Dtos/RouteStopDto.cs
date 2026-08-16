namespace FreshFlow.Logistics.Application.Dtos;

public sealed record RouteStopDto(
    int StopOrder,
    string EntityType,
    Guid EntityId,
    string EntityName,
    decimal Latitude,
    decimal Longitude,
    DateTime? EstimatedArrivalAt,
    DateTime? EstimatedDepartureAt);
