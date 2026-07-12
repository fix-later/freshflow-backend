using FreshFlow.Contracts;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.StartRoute;

internal sealed class StartRouteCommandHandler(
    IDeliveryRouteRepository routes,
    IDeliveryRepository deliveries,
    IHubDiscrepancyStatusReader discrepancies,
    IPublisher publisher)
    : IRequestHandler<StartRouteCommand, Result<StartRouteResultDto>>
{
    public async Task<Result<StartRouteResultDto>> Handle(StartRouteCommand request, CancellationToken ct)
    {
        var route = await routes.FindByIdAsync(request.RouteId, ct);
        if (route is null)
            return Result<StartRouteResultDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.RouteId));

        if (route.DriverUserId != request.DriverUserId)
        {
            return Result<StartRouteResultDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This route is not assigned to the authenticated driver."));
        }

        if (route.Status != RouteStatus.assigned)
        {
            return Result<StartRouteResultDto>.Failure(
                Error.Conflict("ROUTE_NOT_STARTABLE", "Route must be assigned before it can be started."));
        }

        var routeDeliveries = await deliveries.GetByRouteIdsAsync([route.Id], ct);
        if (routeDeliveries.Count == 0)
        {
            return Result<StartRouteResultDto>.Failure(
                Error.Conflict("ROUTE_HAS_NO_DELIVERIES", "Route must be dispatched before it can be started."));
        }

        var orderIds = routeDeliveries.Select(delivery => delivery.OrderId).Distinct().ToList().AsReadOnly();
        var blockedOrderIds = await discrepancies.GetOrdersWithOpenDiscrepanciesAsync(orderIds, ct);
        if (blockedOrderIds.Count > 0)
        {
            return Result<StartRouteResultDto>.Failure(
                Error.Conflict("PENDING_HUB_DISCREPANCY", "Route has orders with pending hub discrepancies."));
        }

        route.Start();
        await routes.SaveChangesAsync(ct);

        await publisher.Publish(
            new DeliveryStartedIntegrationEvent(route.Id, orderIds, DateTime.UtcNow),
            ct);

        return Result<StartRouteResultDto>.Success(
            new StartRouteResultDto(route.Id, route.Status.ToString(), orderIds.Count));
    }
}
