using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.ReviewRoute;

public sealed record ReviewRouteCommand(Guid RouteId, IReadOnlyList<Guid>? StopOrder) : ICommand<RouteDto>;
