using FreshFlow.Contracts;
using FreshFlow.Logistics.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Logistics.Application.EventHandlers;

internal sealed class DeliveryStartedBroadcastHandler(
    IOrderStatusReader orders,
    IDeliveryBroadcastService broadcast,
    ILogger<DeliveryStartedBroadcastHandler> logger)
    : INotificationHandler<DeliveryStartedIntegrationEvent>
{
    public async Task Handle(DeliveryStartedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        foreach (var orderId in notification.OrderIds)
        {
            try
            {
                var order = await orders.FindByIdAsync(orderId, cancellationToken);
                if (order is null)
                {
                    logger.LogWarning(
                        "Skipping delivery-started broadcast because OrderId={OrderId} was not found.",
                        orderId);
                    continue;
                }

                await broadcast.BroadcastDeliveryStartedAsync(
                    order.RestaurantId,
                    new DeliveryRealtimeUpdate(
                        order.OrderId,
                        notification.RouteId,
                        DeliveryId: null,
                        "started",
                        notification.OccurredAt),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Failed to broadcast delivery-started update for RouteId={RouteId}, OrderId={OrderId}.",
                    notification.RouteId,
                    orderId);
            }
        }
    }
}
