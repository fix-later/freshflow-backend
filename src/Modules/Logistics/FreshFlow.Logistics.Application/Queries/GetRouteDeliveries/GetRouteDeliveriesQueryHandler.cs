using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.GetRouteDeliveries;

internal sealed class GetRouteDeliveriesQueryHandler(
    IDeliveryRouteRepository routes,
    IDeliveryRepository deliveries)
    : IRequestHandler<GetRouteDeliveriesQuery, Result<IReadOnlyList<DriverDeliveryDto>>>
{
    public async Task<Result<IReadOnlyList<DriverDeliveryDto>>> Handle(
        GetRouteDeliveriesQuery request,
        CancellationToken ct)
    {
        if (await routes.FindByIdAsync(request.RouteId, ct) is null)
        {
            return Result<IReadOnlyList<DriverDeliveryDto>>.Failure(
                Error.NotFound("DELIVERY_ROUTE", request.RouteId));
        }

        var rows = await deliveries.GetByRouteIdsAsync([request.RouteId], ct);
        return Result<IReadOnlyList<DriverDeliveryDto>>.Success(rows
            .Select(delivery => new DriverDeliveryDto(
                delivery.Id,
                delivery.OrderId,
                delivery.SequenceNumber,
                delivery.Status,
                delivery.EstimatedArrival,
                delivery.ActualArrival,
                delivery.ProofUrl))
            .ToList()
            .AsReadOnly());
    }
}
