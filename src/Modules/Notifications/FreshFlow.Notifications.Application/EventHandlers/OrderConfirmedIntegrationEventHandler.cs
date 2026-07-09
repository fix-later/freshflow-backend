using FreshFlow.Contracts;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Application.EventHandlers;

internal sealed class OrderConfirmedIntegrationEventHandler(
    INotificationRecipientResolver recipients,
    INotificationWriter writer,
    ILogger<OrderConfirmedIntegrationEventHandler> logger)
    : INotificationHandler<OrderConfirmedIntegrationEvent>
{
    public async Task Handle(OrderConfirmedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var userId = await recipients.ResolveUserIdByRestaurantIdAsync(
                notification.RestaurantId,
                cancellationToken);

            if (userId is null)
            {
                logger.LogWarning(
                    "Skipping order-confirmed notification because RestaurantId={RestaurantId} has no owner user.",
                    notification.RestaurantId);
                return;
            }

            await writer.WriteAsync(
                userId.Value,
                NotificationType.order_status,
                "Đơn hàng đã được xác nhận",
                $"Đơn hàng {notification.OrderId} đã được xác nhận.",
                new Dictionary<string, object?>
                {
                    ["order_id"] = notification.OrderId,
                    ["new_status"] = "confirmed",
                    ["total_amount"] = notification.TotalAmount,
                    ["occurred_at"] = notification.OccurredAt,
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to persist order-confirmed notification for OrderId={OrderId}.",
                notification.OrderId);
        }
    }
}
