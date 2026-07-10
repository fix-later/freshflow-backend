using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.CalculateRoute;

public sealed record CalculateRouteCommand(
    IReadOnlyList<Guid> SourceMarketIds,
    IReadOnlyList<Guid> HubIds,
    IReadOnlyList<Guid> DestinationRestaurantIds,
    string? OptimizationCriteria,
    DateOnly ServiceDate,
    bool CompareWithHub) : ICommand<RouteDto>;
