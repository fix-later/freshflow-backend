using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.RegisterVehicle;

internal sealed class RegisterVehicleCommandHandler(IVehicleRepository vehicles)
    : IRequestHandler<RegisterVehicleCommand, Result<VehicleDto>>
{
    public async Task<Result<VehicleDto>> Handle(RegisterVehicleCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<VehicleType>(request.VehicleType, ignoreCase: true, out var vehicleType) ||
            !Enum.IsDefined(vehicleType))
        {
            return Result<VehicleDto>.Failure(
                Error.Validation("VALIDATION_ERROR", "VehicleType must be one of: van, truck, motorbike."));
        }

        if (await vehicles.PlateNumberExistsAsync(request.PlateNumber, excludeId: null, ct))
        {
            return Result<VehicleDto>.Failure(
                Error.Validation(
                    "PLATE_NUMBER_DUPLICATE",
                    $"Vehicle plate number '{request.PlateNumber}' is already registered."));
        }

        var vehicle = new Vehicle(
            request.PlateNumber,
            request.CapacityKg,
            vehicleType,
            request.RegisteredBy);

        await vehicles.AddAsync(vehicle, ct);
        await vehicles.SaveChangesAsync(ct);

        return Result<VehicleDto>.Success(vehicle.ToDto());
    }
}
