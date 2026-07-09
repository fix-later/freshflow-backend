using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.UpdateVehicle;

internal sealed class UpdateVehicleCommandHandler(IVehicleRepository vehicles)
    : IRequestHandler<UpdateVehicleCommand, Result<VehicleDto>>
{
    public async Task<Result<VehicleDto>> Handle(UpdateVehicleCommand request, CancellationToken ct)
    {
        var vehicle = await vehicles.FindByIdAsync(request.Id, ct);
        if (vehicle is null)
            return Result<VehicleDto>.Failure(Error.NotFound("VEHICLE", request.Id));

        if (!Enum.TryParse<VehicleType>(request.VehicleType, ignoreCase: true, out var vehicleType))
        {
            return Result<VehicleDto>.Failure(
                Error.Validation("VALIDATION_ERROR", "VehicleType must be one of: van, truck, motorbike."));
        }

        if (await vehicles.PlateNumberExistsAsync(request.PlateNumber, request.Id, ct))
        {
            return Result<VehicleDto>.Failure(
                Error.Validation(
                    "PLATE_NUMBER_DUPLICATE",
                    $"Vehicle plate number '{request.PlateNumber}' is already registered."));
        }

        vehicle.Update(request.PlateNumber, request.CapacityKg, vehicleType);
        await vehicles.SaveChangesAsync(ct);

        return Result<VehicleDto>.Success(vehicle.ToDto());
    }
}
