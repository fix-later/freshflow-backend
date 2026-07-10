using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.SelectRoute;

internal sealed class SelectRouteCommandHandler(IDeliveryRouteRepository routes)
    : IRequestHandler<SelectRouteCommand, Result<RouteDto>>
{
    public async Task<Result<RouteDto>> Handle(SelectRouteCommand request, CancellationToken ct)
    {
        var route = await routes.FindByIdAsync(request.RouteId, ct);
        if (route is null)
            return Result<RouteDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.RouteId));

        try
        {
            route.Select();
        }
        catch (InvalidOperationException ex)
        {
            return Result<RouteDto>.Failure(Error.Conflict("ROUTE_INVALID_TRANSITION", ex.Message));
        }

        await routes.SaveChangesAsync(ct);
        return Result<RouteDto>.Success(route.ToDto());
    }
}
