using FreshFlow.Contracts;
using FreshFlow.Logistics.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Logistics.Application.EventHandlers;

internal sealed class DeliveryStopUpdatedBroadcastHandler(
    IOrderStatusReader orders,
    IDeliveryBroadcastService broadcast,
    ILogger<DeliveryStopUpdatedBroadcastHandler> logger)
    : INotificationHandler<DeliveryStopUpdatedIntegrationEvent>
{
    public async Task Handle(DeliveryStopUpdatedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var order = await orders.FindByIdAsync(notification.OrderId, cancellationToken);
            if (order is null)
            {
                logger.LogWarning(
                    "Skipping delivery stop broadcast because OrderId={OrderId} was not found.",
                    notification.OrderId);
                return;
            }

            await broadcast.BroadcastDeliveryStopUpdatedAsync(
                order.RestaurantId,
                new DeliveryRealtimeUpdate(
                    order.OrderId,
                    notification.RouteId,
                    notification.DeliveryId,
                    notification.Status,
                    notification.OccurredAt),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to broadcast delivery stop update for DeliveryId={DeliveryId}.",
                notification.DeliveryId);
        }
    }
}
