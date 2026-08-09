using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Abstractions;

public sealed record RoutePoint(decimal Latitude, decimal Longitude);
public sealed record ProfileRouteMatrix(long[][] DistanceMeters, long[][] DurationSeconds);
public sealed record RouteMatrixResult(
    IReadOnlyDictionary<string, ProfileRouteMatrix> Profiles,
    string Provider,
    bool IsEstimated,
    IReadOnlyList<string> Warnings);

public interface IRouteMatrixProvider
{
    public Task<RouteMatrixResult> GetMatrixAsync(
        IReadOnlyList<RoutePoint> points,
        IReadOnlyCollection<string> vehicleProfiles,
        CancellationToken cancellationToken);
}

public sealed record RestaurantDemand(
    Guid RestaurantId,
    string RestaurantName,
    IReadOnlyList<Guid> OrderIds,
    decimal Latitude,
    decimal Longitude,
    decimal LoadKg);

public sealed record PlanningVehicle(
    Guid Id,
    string PlateNumber,
    VehicleType VehicleType,
    string RoutingProfile,
    decimal CapacityKg,
    decimal EffectiveCapacityKg);

public sealed record RoutePlanningInput(
    Guid HubId,
    string HubName,
    decimal HubLatitude,
    decimal HubLongitude,
    DateOnly ServiceDate,
    IReadOnlyList<RestaurantDemand> Demands,
    IReadOnlyList<PlanningVehicle> Vehicles,
    string InputRevision);

public interface IRoutePlanningInputBuilder
{
    public Task<Result<RoutePlanningInput>> BuildAsync(
        Guid hubId, DateOnly serviceDate, CancellationToken cancellationToken);
}

public sealed record SolvedVehicleRoute(
    PlanningVehicle Vehicle,
    IReadOnlyList<RestaurantDemand> Restaurants,
    long DistanceMeters,
    long RoadDurationSeconds);

public sealed record RoutePlanningSolution(
    IReadOnlyList<SolvedVehicleRoute> Routes,
    IReadOnlyList<RoutePlanUnassigned> Unassigned);

public interface IRoutePlanningSolver
{
    public RoutePlanningSolution Solve(
        RoutePlanningInput input,
        RouteMatrixResult matrix,
        OptimizationCriteria criteria);
}
