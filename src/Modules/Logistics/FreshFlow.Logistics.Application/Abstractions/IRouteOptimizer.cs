using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;

namespace FreshFlow.Logistics.Application.Abstractions;

public interface IRouteOptimizer
{
    public RouteOptimizationResult Optimize(
        IReadOnlyList<RouteStop> stops,
        DateOnly serviceDate,
        OptimizationCriteria criteria);

    public RouteOptimizationResult Recalculate(
        IReadOnlyList<RouteStop> stops,
        DateOnly serviceDate);
}

public sealed record RouteOptimizationResult(
    IReadOnlyList<RouteStop> Stops,
    decimal TotalDistanceKm,
    int EstimatedDurationMinutes,
    decimal EstimatedCost);
