using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.CalculateRoute;

public sealed record CalculateRouteCommand(
    Guid HubId,
    IReadOnlyList<Guid> DestinationRestaurantIds,
    string? OptimizationCriteria,
    DateOnly ServiceDate) : ICommand<RouteDto>;
