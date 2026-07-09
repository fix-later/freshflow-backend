using FreshFlow.Contracts;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Application.EventHandlers;

internal sealed class OrderCancelledIntegrationEventHandler(
    INotificationRecipientResolver recipients,
    INotificationWriter writer,
    ILogger<OrderCancelledIntegrationEventHandler> logger)
    : INotificationHandler<OrderCancelledIntegrationEvent>
{
    public async Task Handle(OrderCancelledIntegrationEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var userId = await recipients.ResolveUserIdByRestaurantIdAsync(
                notification.RestaurantId,
                cancellationToken);

            if (userId is null)
            {
                logger.LogWarning(
                    "Skipping order-cancelled notification because RestaurantId={RestaurantId} has no owner user.",
                    notification.RestaurantId);
                return;
            }

            await writer.WriteAsync(
                userId.Value,
                NotificationType.order_status,
                "Đơn hàng đã bị hủy",
                $"Đơn hàng {notification.OrderId} đã bị hủy.",
                new Dictionary<string, object?>
                {
                    ["order_id"] = notification.OrderId,
                    ["new_status"] = "cancelled",
                    ["cancellation_reason"] = notification.CancellationReason,
                    ["occurred_at"] = notification.OccurredAt,
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to persist order-cancelled notification for OrderId={OrderId}.",
                notification.OrderId);
        }
    }
}
