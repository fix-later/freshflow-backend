using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.ReorderDriverRoute;

public sealed record ReorderDriverRouteCommand(
    Guid RouteId,
    Guid DriverUserId,
    IReadOnlyList<Guid> StopOrder) : IRequest<Result<RouteDto>>;
