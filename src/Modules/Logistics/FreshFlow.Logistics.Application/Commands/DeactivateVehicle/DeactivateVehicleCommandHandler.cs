using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.DeactivateVehicle;

internal sealed class DeactivateVehicleCommandHandler(IVehicleRepository vehicles)
    : IRequestHandler<DeactivateVehicleCommand, Result<VehicleDto>>
{
    public async Task<Result<VehicleDto>> Handle(DeactivateVehicleCommand request, CancellationToken ct)
    {
        var vehicle = await vehicles.FindByIdAsync(request.Id, ct);
        if (vehicle is null)
            return Result<VehicleDto>.Failure(Error.NotFound("VEHICLE", request.Id));

        vehicle.Deactivate();
        await vehicles.SaveChangesAsync(ct);

        return Result<VehicleDto>.Success(vehicle.ToDto());
    }
}
