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

    public Task<RouteOptimizationResult> OptimizeAsync(
        IReadOnlyList<RouteStop> stops, DateOnly serviceDate, OptimizationCriteria criteria,
        string routingProfile, CancellationToken cancellationToken) =>
        Task.FromResult(Optimize(stops, serviceDate, criteria));

    public Task<RouteOptimizationResult> RecalculateAsync(
        IReadOnlyList<RouteStop> stops, DateOnly serviceDate, string routingProfile,
        CancellationToken cancellationToken) =>
        Task.FromResult(Recalculate(stops, serviceDate));
}

public sealed record RouteOptimizationResult(
    IReadOnlyList<RouteStop> Stops,
    decimal TotalDistanceKm,
    int EstimatedDurationMinutes,
    decimal EstimatedCost);
