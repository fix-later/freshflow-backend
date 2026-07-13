using FreshFlow.Contracts;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Application.EventHandlers;

internal sealed class DeliveryCompletedIntegrationEventHandler(
    INotificationRecipientResolver recipients,
    INotificationWriter writer,
    ILogger<DeliveryCompletedIntegrationEventHandler> logger)
    : INotificationHandler<DeliveryCompletedIntegrationEvent>
{
    public async Task Handle(DeliveryCompletedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var userId = await recipients.ResolveUserIdByOrderIdAsync(notification.OrderId, cancellationToken);
            if (userId is null)
            {
                logger.LogWarning(
                    "Skipping delivery-completed notification because OrderId={OrderId} has no recipient.",
                    notification.OrderId);
                return;
            }

            await writer.WriteAsync(
                userId.Value,
                NotificationType.delivery_update,
                "Đơn hàng đã được giao",
                $"Đơn hàng {notification.OrderId} đã giao thành công.",
                new Dictionary<string, object?>
                {
                    ["order_id"] = notification.OrderId,
                    ["route_id"] = notification.RouteId,
                    ["status"] = "delivered",
                    ["actual_arrival_at"] = notification.ActualArrivalAt,
                    ["occurred_at"] = notification.OccurredAt,
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to persist delivery-completed notification for OrderId={OrderId}.",
                notification.OrderId);
        }
    }
}
