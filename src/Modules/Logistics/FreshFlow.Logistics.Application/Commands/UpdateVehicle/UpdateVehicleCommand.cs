using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.UpdateVehicle;

public sealed record UpdateVehicleCommand(
    Guid Id,
    string PlateNumber,
    decimal CapacityKg,
    string VehicleType) : IRequest<Result<VehicleDto>>;
