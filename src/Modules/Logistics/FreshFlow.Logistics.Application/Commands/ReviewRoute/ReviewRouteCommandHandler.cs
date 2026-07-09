using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.ReviewRoute;

internal sealed class ReviewRouteCommandHandler(
    IDeliveryRouteRepository routes,
    IRouteOptimizer optimizer)
    : IRequestHandler<ReviewRouteCommand, Result<RouteDto>>
{
    public async Task<Result<RouteDto>> Handle(ReviewRouteCommand request, CancellationToken ct)
    {
        var route = await routes.FindByIdAsync(request.RouteId, ct);
        if (route is null)
            return Result<RouteDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.RouteId));

        if (route.OptimizationCriteria is null)
        {
            return Result<RouteDto>.Failure(Error.Conflict(
                "ROUTE_INVALID_TRANSITION",
                "Route must be optimized before it can be reviewed."));
        }

        try
        {
            if (request.StopOrder is { Count: > 0 })
            {
                route.AdjustStopOrder(request.StopOrder);
                var recalculated = optimizer.Recalculate(route.Stops, route.ServiceDate);
                route.ApplyOptimization(
                    recalculated.Stops,
                    recalculated.TotalDistanceKm,
                    recalculated.EstimatedDurationMinutes,
                    recalculated.EstimatedCost,
                    route.OptimizationCriteria.Value);
            }

            route.MarkReviewed();
        }
        catch (ArgumentException ex)
        {
            return Result<RouteDto>.Failure(Error.Validation("INVALID_STOP_ORDER", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result<RouteDto>.Failure(Error.Conflict("ROUTE_INVALID_TRANSITION", ex.Message));
        }

        await routes.SaveChangesAsync(ct);
        return Result<RouteDto>.Success(route.ToDto());
    }
}
