using FreshFlow.Contracts;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Application.EventHandlers;

internal sealed class CreditLimitThresholdReachedIntegrationEventHandler(
    INotificationRecipientResolver recipients,
    INotificationWriter writer,
    ILogger<CreditLimitThresholdReachedIntegrationEventHandler> logger)
    : INotificationHandler<CreditLimitThresholdReachedIntegrationEvent>
{
    public async Task Handle(
        CreditLimitThresholdReachedIntegrationEvent notification,
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
                    "Skipping credit-limit notification because RestaurantId={RestaurantId} has no owner user.",
                    notification.RestaurantId);
                return;
            }

            await writer.WriteAsync(
                userId.Value,
                NotificationType.credit_alert,
                "Cảnh báo hạn mức tín dụng",
                BuildBody(notification),
                new Dictionary<string, object?>
                {
                    ["level"] = notification.Level,
                    ["utilization"] = notification.Utilization,
                    ["outstanding"] = notification.OutstandingBalance,
                    ["limit"] = notification.CreditLimit,
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to persist credit-limit notification for RestaurantId={RestaurantId}.",
                notification.RestaurantId);
        }
    }

    private static string BuildBody(CreditLimitThresholdReachedIntegrationEvent notification) =>
        $"Mức sử dụng tín dụng đã đạt {notification.Utilization:P0}. " +
        $"Dư nợ hiện tại {notification.OutstandingBalance:N0} / hạn mức {notification.CreditLimit:N0}.";
}
