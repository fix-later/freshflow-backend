using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.OptimizeRoute;

public sealed record OptimizeRouteCommand(Guid RouteId, string OptimizationCriteria) : ICommand<RouteDto>;
