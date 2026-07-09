using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.DeliveryZones.Update;

internal sealed class UpdateDeliveryZoneCommandHandler(IDeliveryZoneRepository zones)
    : IRequestHandler<UpdateDeliveryZoneCommand, Result<DeliveryZoneDto>>
{
    public async Task<Result<DeliveryZoneDto>> Handle(UpdateDeliveryZoneCommand request, CancellationToken ct)
    {
        var zone = await zones.FindByIdAsync(request.Id, ct);
        if (zone is null)
            return Result<DeliveryZoneDto>.Failure(Error.NotFound("DELIVERY_ZONE", request.Id));

        zone.Update(request.Name, request.Description);
        await zones.SaveChangesAsync(ct);

        return Result<DeliveryZoneDto>.Success(zone.ToDto());
    }
}
