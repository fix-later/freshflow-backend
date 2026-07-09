using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.OptimizeRoute;

internal sealed class OptimizeRouteCommandHandler(
    IDeliveryRouteRepository routes,
    IRouteOptimizer optimizer)
    : IRequestHandler<OptimizeRouteCommand, Result<RouteDto>>
{
    public async Task<Result<RouteDto>> Handle(OptimizeRouteCommand request, CancellationToken ct)
    {
        var route = await routes.FindByIdAsync(request.RouteId, ct);
        if (route is null)
            return Result<RouteDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.RouteId));

        if (!IsValidCriteria(request.OptimizationCriteria) ||
            !Enum.TryParse<OptimizationCriteria>(request.OptimizationCriteria, ignoreCase: true, out var criteria))
        {
            return Result<RouteDto>.Failure(Error.Validation(
                "VALIDATION_ERROR",
                "OptimizationCriteria must be DISTANCE, TIME, or COST."));
        }

        var result = optimizer.Optimize(route.Stops, route.ServiceDate, criteria);

        try
        {
            route.ApplyOptimization(
                result.Stops,
                result.TotalDistanceKm,
                result.EstimatedDurationMinutes,
                result.EstimatedCost,
                criteria);
        }
        catch (InvalidOperationException ex)
        {
            return Result<RouteDto>.Failure(Error.Conflict("ROUTE_INVALID_TRANSITION", ex.Message));
        }

        await routes.SaveChangesAsync(ct);
        return Result<RouteDto>.Success(route.ToDto());
    }

    private static bool IsValidCriteria(string criteria) =>
        Enum.GetNames<OptimizationCriteria>()
            .Any(name => string.Equals(name, criteria, StringComparison.OrdinalIgnoreCase));
}
