using FreshFlow.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Application.EventHandlers;

/// <summary>
/// SCRUM-266 Option B stub: observe credit-limit alerts without bootstrapping
/// notification persistence yet. Replace with a persisted notification writer in
/// the Notifications epic.
/// </summary>
internal sealed class CreditLimitThresholdReachedIntegrationEventHandler(
    ILogger<CreditLimitThresholdReachedIntegrationEventHandler> logger)
    : INotificationHandler<CreditLimitThresholdReachedIntegrationEvent>
{
    public Task Handle(
        CreditLimitThresholdReachedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "[STUB] Credit limit alert for RestaurantId={RestaurantId}: Level={Level}, Utilization={Utilization:P2}, Outstanding={OutstandingBalance}, Limit={CreditLimit}. Notification persistence deferred.",
            notification.RestaurantId,
            notification.Level,
            notification.Utilization,
            notification.OutstandingBalance,
            notification.CreditLimit);

        return Task.CompletedTask;
    }
}
