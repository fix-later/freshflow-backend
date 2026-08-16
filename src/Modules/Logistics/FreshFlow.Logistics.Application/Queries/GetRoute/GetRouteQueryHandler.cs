using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.GetRoute;

internal sealed class GetRouteQueryHandler(IDeliveryRouteRepository routes)
    : IRequestHandler<GetRouteQuery, Result<RouteDto>>
{
    public async Task<Result<RouteDto>> Handle(GetRouteQuery request, CancellationToken ct)
    {
        var route = await routes.FindByIdAsync(request.Id, ct);
        return route is null
            ? Result<RouteDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.Id))
            : Result<RouteDto>.Success(route.ToDto());
    }
}
