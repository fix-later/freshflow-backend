using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.ReorderDriverRoute;

internal sealed class ReorderDriverRouteCommandHandler(
    IDeliveryRouteRepository routes,
    IRouteOptimizer optimizer)
    : IRequestHandler<ReorderDriverRouteCommand, Result<RouteDto>>
{
    public async Task<Result<RouteDto>> Handle(
        ReorderDriverRouteCommand request,
        CancellationToken ct)
    {
        var route = await routes.FindByIdAsync(request.RouteId, ct);
        if (route is null)
            return Result<RouteDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.RouteId));

        if (route.DriverUserId != request.DriverUserId)
        {
            return Result<RouteDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This route is not assigned to the authenticated driver."));
        }

        if (route.Status != RouteStatus.assigned)
        {
            return Result<RouteDto>.Failure(Error.Conflict(
                "ROUTE_NOT_REORDERABLE",
                "Route can only be reordered while assigned, before departure."));
        }

        try
        {
            route.ReorderStopsByDriver(request.StopOrder);
            var recalculated = optimizer.Recalculate(route.Stops, route.ServiceDate);
            route.ApplyDriverRecalculation(
                recalculated.Stops,
                recalculated.TotalDistanceKm,
                recalculated.EstimatedDurationMinutes,
                recalculated.EstimatedCost);
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
