using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.AssignVehicleToHub;

internal sealed class AssignVehicleToHubCommandHandler(
    IVehicleRepository vehicles,
    IHubCoordinateReader hubs)
    : IRequestHandler<AssignVehicleToHubCommand, Result<VehicleDto>>
{
    public async Task<Result<VehicleDto>> Handle(
        AssignVehicleToHubCommand request,
        CancellationToken ct)
    {
        if (await hubs.FindByIdAsync(request.HubId, ct) is null)
        {
            return Result<VehicleDto>.Failure(Error.NotFound("HUB", request.HubId));
        }

        var vehicle = await vehicles.FindByIdAsync(request.VehicleId, ct);
        if (vehicle is null)
            return Result<VehicleDto>.Failure(Error.NotFound("VEHICLE", request.VehicleId));

        vehicle.AssignHub(request.HubId);
        await vehicles.SaveChangesAsync(ct);
        return Result<VehicleDto>.Success(vehicle.ToDto());
    }
}
