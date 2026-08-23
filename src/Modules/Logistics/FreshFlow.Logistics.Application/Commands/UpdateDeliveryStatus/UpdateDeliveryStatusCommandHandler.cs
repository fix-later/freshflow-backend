using FreshFlow.Contracts;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.UpdateDeliveryStatus;

internal sealed class UpdateDeliveryStatusCommandHandler(
    IDeliveryRepository deliveries,
    IDeliveryRouteRepository routes,
    IPublisher publisher)
    : IRequestHandler<UpdateDeliveryStatusCommand, Result<UpdateDeliveryStatusResponse>>
{
    private const string RequestedArrived = "ARRIVED";
    private const string RequestedDelivered = "DELIVERED";
    private const string RequestedFailed = "FAILED";

    public async Task<Result<UpdateDeliveryStatusResponse>> Handle(
        UpdateDeliveryStatusCommand request,
        CancellationToken ct)
    {
        var delivery = await deliveries.FindByIdAsync(request.DeliveryId, ct);
        if (delivery is null)
            return Result<UpdateDeliveryStatusResponse>.Failure(Error.NotFound("DELIVERY", request.DeliveryId));

        var route = await routes.FindByIdAsync(delivery.DeliveryRouteId, ct);
        if (route is null)
        {
            return Result<UpdateDeliveryStatusResponse>.Failure(
                Error.NotFound("DELIVERY_ROUTE", delivery.DeliveryRouteId));
        }

        if (route.DriverUserId != request.DriverUserId)
        {
            return Result<UpdateDeliveryStatusResponse>.Failure(
                Error.Unauthorized("FORBIDDEN", "This delivery is not assigned to the authenticated driver."));
        }

        if (route.Status != RouteStatus.in_progress)
        {
            return Result<UpdateDeliveryStatusResponse>.Failure(
                Error.Conflict("DELIVERY_ROUTE_NOT_IN_PROGRESS", "Route must be in progress to update delivery status."));
        }

        if (!CanTransition(delivery.Status, request.Status))
        {
            return Result<UpdateDeliveryStatusResponse>.Failure(
                Error.Conflict("DELIVERY_STATUS_INVALID", "Delivery status transition is invalid."));
        }

        var routeDeliveries = await deliveries.GetByRouteIdsAsync([route.Id], ct);
        var completesRoute = request.Status is RequestedDelivered or RequestedFailed &&
                             route.Status == RouteStatus.in_progress &&
                             routeDeliveries.Count > 0 &&
                             routeDeliveries.All(d =>
                                 d.Id == delivery.Id || IsTerminal(d.Status));

        switch (request.Status)
        {
            case RequestedArrived:
                delivery.MarkArrived();
                break;
            case RequestedDelivered:
                delivery.MarkDelivered(DateTime.UtcNow);
                break;
            case RequestedFailed:
                delivery.MarkFailed(request.FailureReason!);
                break;
        }

        if (completesRoute)
            route.Complete();

        await deliveries.SaveChangesAsync(ct);

        if (request.Status == RequestedDelivered)
        {
            await publisher.Publish(
                new DeliveryCompletedIntegrationEvent(
                    delivery.OrderId,
                    route.Id,
                    delivery.ActualArrival!.Value,
                    DateTime.UtcNow),
                CancellationToken.None);
        }

        if (request.Status == RequestedFailed)
        {
            await publisher.Publish(
                new DeliveryFailedIntegrationEvent(
                    delivery.OrderId,
                    route.Id,
                    delivery.Id,
                    delivery.FailureReason!,
                    DateTime.UtcNow),
                CancellationToken.None);
        }

        await publisher.Publish(
            new DeliveryStopUpdatedIntegrationEvent(
                delivery.OrderId,
                route.Id,
                delivery.Id,
                delivery.Status,
                DateTime.UtcNow),
            CancellationToken.None);

        return Result<UpdateDeliveryStatusResponse>.Success(
            new UpdateDeliveryStatusResponse(
                delivery.Id,
                route.Id,
                delivery.Status,
                delivery.ActualArrival,
                delivery.FailureReason));
    }

    private static bool CanTransition(string currentStatus, string requestedStatus) =>
        currentStatus switch
        {
            Delivery.StatusPending => requestedStatus is RequestedArrived or RequestedFailed,
            Delivery.StatusArrived => requestedStatus is RequestedDelivered or RequestedFailed,
            _ => false
        };

    private static bool IsTerminal(string status) =>
        status is Delivery.StatusDelivered or Delivery.StatusFailed;
}
