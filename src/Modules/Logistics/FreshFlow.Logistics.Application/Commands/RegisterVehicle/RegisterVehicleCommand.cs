using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.RegisterVehicle;

public sealed record RegisterVehicleCommand(
    string PlateNumber,
    decimal CapacityKg,
    string VehicleType,
    Guid? RegisteredBy) : IRequest<Result<VehicleDto>>;
