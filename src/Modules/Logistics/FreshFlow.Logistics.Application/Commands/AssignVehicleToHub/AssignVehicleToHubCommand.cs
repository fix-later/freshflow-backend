using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.AssignVehicleToHub;

public sealed record AssignVehicleToHubCommand(
    Guid VehicleId,
    Guid HubId) : IRequest<Result<VehicleDto>>;
