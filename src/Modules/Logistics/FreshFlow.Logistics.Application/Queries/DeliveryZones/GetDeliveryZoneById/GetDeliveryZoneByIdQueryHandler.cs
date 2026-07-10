using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.DeliveryZones.GetDeliveryZoneById;

internal sealed class GetDeliveryZoneByIdQueryHandler(IDeliveryZoneRepository zones)
    : IRequestHandler<GetDeliveryZoneByIdQuery, Result<DeliveryZoneDto>>
{
    public async Task<Result<DeliveryZoneDto>> Handle(GetDeliveryZoneByIdQuery request, CancellationToken ct)
    {
        var zone = await zones.FindByIdAsync(request.Id, ct);
        return zone is null
            ? Result<DeliveryZoneDto>.Failure(Error.NotFound("DELIVERY_ZONE", request.Id))
            : Result<DeliveryZoneDto>.Success(zone.ToDto());
    }
}
