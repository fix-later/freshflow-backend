using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.DeliveryZones.Create;

internal sealed class CreateDeliveryZoneCommandHandler(IDeliveryZoneRepository zones)
    : IRequestHandler<CreateDeliveryZoneCommand, Result<DeliveryZoneDto>>
{
    public async Task<Result<DeliveryZoneDto>> Handle(CreateDeliveryZoneCommand request, CancellationToken ct)
    {
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await zones.CodeExistsAsync(normalizedCode, ct))
        {
            return Result<DeliveryZoneDto>.Failure(
                Error.Conflict(
                    "DELIVERY_ZONE_CODE_EXISTS",
                    $"Delivery zone code '{normalizedCode}' already exists."));
        }

        var zone = new DeliveryZone(request.Code, request.Name, request.Description);
        await zones.AddAsync(zone, ct);
        await zones.SaveChangesAsync(ct);

        return Result<DeliveryZoneDto>.Success(zone.ToDto());
    }
}
