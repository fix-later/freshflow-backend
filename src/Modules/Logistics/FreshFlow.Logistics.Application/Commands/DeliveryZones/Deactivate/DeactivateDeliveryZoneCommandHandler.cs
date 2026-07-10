using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.DeliveryZones.Deactivate;

internal sealed class DeactivateDeliveryZoneCommandHandler(IDeliveryZoneRepository zones)
    : IRequestHandler<DeactivateDeliveryZoneCommand, Result<DeliveryZoneDto>>
{
    public async Task<Result<DeliveryZoneDto>> Handle(DeactivateDeliveryZoneCommand request, CancellationToken ct)
    {
        var zone = await zones.FindByIdAsync(request.Id, ct);
        if (zone is null)
            return Result<DeliveryZoneDto>.Failure(Error.NotFound("DELIVERY_ZONE", request.Id));

        zone.Deactivate();
        await zones.SaveChangesAsync(ct);

        return Result<DeliveryZoneDto>.Success(zone.ToDto());
    }
}
