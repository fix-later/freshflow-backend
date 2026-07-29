using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.PlanRoutes;

public sealed record PlanRoutesCommand(
    Guid HubId,
    DateOnly ServiceDate,
    string? OptimizationCriteria) : ICommand<IReadOnlyList<RouteDto>>;
