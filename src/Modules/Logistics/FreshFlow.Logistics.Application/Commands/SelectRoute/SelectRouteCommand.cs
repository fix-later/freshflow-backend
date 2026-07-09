using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.SelectRoute;

public sealed record SelectRouteCommand(Guid RouteId) : ICommand<RouteDto>;
