using FreshFlow.Contracts;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Application.EventHandlers;

internal sealed class RestaurantRefundIssuedIntegrationEventHandler(
    INotificationRecipientResolver recipients,
    INotificationWriter writer,
    ILogger<RestaurantRefundIssuedIntegrationEventHandler> logger)
    : INotificationHandler<RestaurantRefundIssuedIntegrationEvent>
{
    public async Task Handle(
        RestaurantRefundIssuedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = await recipients.ResolveUserIdByRestaurantIdAsync(
                notification.RestaurantId,
                cancellationToken);

            if (userId is null)
            {
                logger.LogWarning(
                    "Skipping refund notification because RestaurantId={RestaurantId} has no owner user.",
                    notification.RestaurantId);
                return;
            }

            await writer.WriteAsync(
                userId.Value,
                NotificationType.credit_alert,
                "Đã hoàn tiền do sai lệch tại hub",
                $"Đơn hàng {notification.OrderId} đã được hoàn {notification.RefundAmount:N0}.",
                new Dictionary<string, object?>
                {
                    ["order_id"] = notification.OrderId,
                    ["item_name"] = notification.OrderItemName,
                    ["affected_quantity"] = notification.AffectedQuantity,
                    ["refund_amount"] = notification.RefundAmount,
                    ["occurred_at"] = notification.OccurredAt,
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to persist refund notification for OrderId={OrderId}.",
                notification.OrderId);
        }
    }
}
