using FreshFlow.Contracts;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Application.EventHandlers;

internal sealed class DeliveryStartedIntegrationEventHandler(
    INotificationRecipientResolver recipients,
    INotificationWriter writer,
    ILogger<DeliveryStartedIntegrationEventHandler> logger)
    : INotificationHandler<DeliveryStartedIntegrationEvent>
{
    public async Task Handle(DeliveryStartedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        foreach (var orderId in notification.OrderIds)
        {
            try
            {
                var userId = await recipients.ResolveUserIdByOrderIdAsync(orderId, cancellationToken);
                if (userId is null)
                {
                    logger.LogWarning(
                        "Skipping delivery-started notification because OrderId={OrderId} has no recipient.",
                        orderId);
                    continue;
                }

                await writer.WriteAsync(
                    userId.Value,
                    NotificationType.delivery_update,
                    "Đơn hàng đang được giao",
                    $"Đơn hàng {orderId} đang trên đường giao.",
                    new Dictionary<string, object?>
                    {
                        ["order_id"] = orderId,
                        ["route_id"] = notification.RouteId,
                        ["status"] = "started",
                        ["occurred_at"] = notification.OccurredAt,
                    },
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Failed to persist delivery-started notification for RouteId={RouteId}, OrderId={OrderId}.",
                    notification.RouteId,
                    orderId);
            }
        }
    }
}
