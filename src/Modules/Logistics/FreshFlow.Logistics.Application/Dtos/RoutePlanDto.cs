namespace FreshFlow.Logistics.Application.Dtos;

public sealed record RoutePlanDto(
    Guid? PlanId, string Status, Guid HubId, DateOnly ServiceDate,
    string OptimizationCriteria, string RoutingProvider, bool IsEstimated,
    string InputRevision, int VehiclesUsed, decimal TotalLoadKg,
    decimal TotalDistanceKm, int EstimatedDurationMinutes, decimal EstimatedCost,
    IReadOnlyList<PlannedRouteDto> Routes,
    IReadOnlyList<UnassignedRouteDemandDto> Unassigned,
    IReadOnlyList<string> Warnings, DateTime CreatedAt);

public sealed record PlannedRouteDto(
    Guid RouteId, Guid SuggestedVehicleId, string PlateNumber, string VehicleType,
    decimal PlannedLoadKg, decimal EffectiveCapacityKg, decimal UtilizationPercent,
    decimal TotalDistanceKm, int EstimatedDurationMinutes, decimal EstimatedCost,
    DateTime EstimatedReturnAt, IReadOnlyList<PlannedRouteStopDto> Stops);

public sealed record PlannedRouteStopDto(
    int StopOrder, Guid RestaurantId, string RestaurantName,
    IReadOnlyList<Guid> OrderIds, decimal LoadKg,
    DateTime? EstimatedArrivalAt, DateTime? EstimatedDepartureAt);

public sealed record UnassignedRouteDemandDto(
    Guid RestaurantId, string RestaurantName, IReadOnlyList<Guid> OrderIds,
    decimal LoadKg, string Reason, bool ExcludedForIncompleteData = false);
