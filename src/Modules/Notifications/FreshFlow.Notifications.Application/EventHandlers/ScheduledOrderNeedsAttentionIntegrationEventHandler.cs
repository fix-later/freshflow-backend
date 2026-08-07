using FreshFlow.Contracts;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Application.EventHandlers;

internal sealed class ScheduledOrderNeedsAttentionIntegrationEventHandler(
    INotificationRecipientResolver recipients,
    INotificationWriter writer,
    ILogger<ScheduledOrderNeedsAttentionIntegrationEventHandler> logger)
    : INotificationHandler<ScheduledOrderNeedsAttentionIntegrationEvent>
{
    public async Task Handle(
        ScheduledOrderNeedsAttentionIntegrationEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var userId = await recipients.ResolveUserIdByRestaurantIdAsync(
                notification.RestaurantId,
                cancellationToken);

            if (userId is null)
            {
                logger.LogWarning(
                    "Skipping scheduled-order-needs-attention notification because RestaurantId={RestaurantId} has no owner user.",
                    notification.RestaurantId);
                return;
            }

            await writer.WriteAsync(
                userId.Value,
                NotificationType.system,
                "Đơn định kỳ cần xử lý tay",
                $"Đơn định kỳ ngày {notification.OccurrenceDate:dd/MM/yyyy} chưa thể tự đặt: {notification.Reason}",
                new Dictionary<string, object?>
                {
                    ["scheduled_order_id"] = notification.ScheduledOrderId,
                    ["order_id"] = notification.OrderId,
                    ["occurrence_date"] = notification.OccurrenceDate,
                    ["reason"] = notification.Reason,
                    ["occurred_at"] = notification.OccurredAt,
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to persist scheduled-order-needs-attention notification for ScheduledOrderId={ScheduledOrderId}.",
                notification.ScheduledOrderId);
        }
    }
}
