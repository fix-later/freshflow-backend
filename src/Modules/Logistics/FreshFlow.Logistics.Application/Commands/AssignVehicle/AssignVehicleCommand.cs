using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.AssignVehicle;

public sealed record AssignVehicleCommand(
    Guid RouteId,
    Guid VehicleId,
    Guid? DriverUserId) : ICommand<RouteDto>;
