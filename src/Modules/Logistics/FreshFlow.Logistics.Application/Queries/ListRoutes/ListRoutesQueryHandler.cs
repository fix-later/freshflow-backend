using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.ListRoutes;

internal sealed class ListRoutesQueryHandler(IDeliveryRouteRepository routes)
    : IRequestHandler<ListRoutesQuery, Result<RoutePageDto>>
{
    public async Task<Result<RoutePageDto>> Handle(ListRoutesQuery request, CancellationToken ct)
    {
        RouteStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<RouteStatus>(request.Status, ignoreCase: true, out var parsed))
            {
                return Result<RoutePageDto>.Failure(Error.Validation(
                    "VALIDATION_ERROR",
                    "Status must be one of: planned, selected, reviewed, assigned, cancelled."));
            }

            status = parsed;
        }

        var (items, nextCursor) = await routes.GetPageAsync(
            request.Cursor,
            request.PageSize,
            request.ServiceDate,
            status,
            ct);

        var dtos = items.Select(route => route.ToDto()).ToList().AsReadOnly();
        return Result<RoutePageDto>.Success(new RoutePageDto(dtos, request.PageSize, nextCursor));
    }
}
