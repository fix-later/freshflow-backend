using FreshFlow.Contracts;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Domain.Events;
using MediatR;

namespace FreshFlow.Orders.Application.EventHandlers;

/// <summary>
/// Translates <see cref="CreditLimitThresholdReachedDomainEvent"/> into
/// <see cref="CreditLimitThresholdReachedIntegrationEvent"/> for cross-module consumption
/// (Notifications persists an alert notification for the restaurant).
/// </summary>
internal sealed class CreditLimitThresholdReachedDomainEventHandler(IPublisher publisher)
    : INotificationHandler<CreditLimitThresholdReachedDomainEvent>
{
    public Task Handle(CreditLimitThresholdReachedDomainEvent notification, CancellationToken cancellationToken) =>
        publisher.Publish(
            new CreditLimitThresholdReachedIntegrationEvent(
                notification.RestaurantId,
                ToSnakeCase(notification.Level),
                notification.Utilization,
                notification.OutstandingBalance,
                notification.CreditLimit,
                notification.OccurredAt),
            cancellationToken);

    private static string ToSnakeCase(CreditAlertLevel level) => level switch
    {
        CreditAlertLevel.Warning => "warning",
        CreditAlertLevel.Exceeded => "exceeded",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };
}
