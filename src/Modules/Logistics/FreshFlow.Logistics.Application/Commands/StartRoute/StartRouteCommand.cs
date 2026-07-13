using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.StartRoute;

public sealed record StartRouteCommand(Guid RouteId, Guid DriverUserId) : ICommand<StartRouteResultDto>;
