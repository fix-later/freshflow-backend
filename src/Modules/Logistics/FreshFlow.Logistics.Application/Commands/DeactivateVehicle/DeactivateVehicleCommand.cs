using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.DeactivateVehicle;

public sealed record DeactivateVehicleCommand(Guid Id) : IRequest<Result<VehicleDto>>;
