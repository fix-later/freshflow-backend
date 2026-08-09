using FreshFlow.Logistics.Domain.Enums;

namespace FreshFlow.Logistics.Domain.ValueObjects;

public sealed record RouteStop(
    int StopOrder,
    StopEntityType EntityType,
    Guid EntityId,
    string EntityName,
    decimal Latitude,
    decimal Longitude,
    DateTime? EstimatedArrivalAt,
    DateTime? EstimatedDepartureAt,
    IReadOnlyList<Guid>? OrderIds = null,
    decimal? LoadKg = null);
