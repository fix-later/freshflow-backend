using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.DeliveryZones.GetDeliveryZones;

internal sealed class GetDeliveryZonesQueryHandler(IDeliveryZoneRepository zones)
    : IRequestHandler<GetDeliveryZonesQuery, Result<IReadOnlyList<DeliveryZoneDto>>>
{
    public async Task<Result<IReadOnlyList<DeliveryZoneDto>>> Handle(
        GetDeliveryZonesQuery request,
        CancellationToken ct)
    {
        var rows = await zones.GetAllAsync(request.ActiveOnly, ct);
        var dtos = rows.Select(zone => zone.ToDto()).ToList().AsReadOnly();
        return Result<IReadOnlyList<DeliveryZoneDto>>.Success(dtos);
    }
}
