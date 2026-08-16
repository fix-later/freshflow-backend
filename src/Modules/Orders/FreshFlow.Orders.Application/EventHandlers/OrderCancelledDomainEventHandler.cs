using FreshFlow.Contracts;
using FreshFlow.Orders.Domain.Events;
using MediatR;

namespace FreshFlow.Orders.Application.EventHandlers;

/// <summary>
/// Translates <see cref="OrderCancelledDomainEvent"/> into <see cref="OrderCancelledIntegrationEvent"/>
/// for cross-module consumption (Logistics cancels the delivery record, Notifications notifies the restaurant).
/// </summary>
internal sealed class OrderCancelledDomainEventHandler(IPublisher publisher)
    : INotificationHandler<OrderCancelledDomainEvent>
{
    public Task Handle(OrderCancelledDomainEvent notification, CancellationToken cancellationToken) =>
        publisher.Publish(
            new OrderCancelledIntegrationEvent(
                notification.OrderId,
                notification.RestaurantId,
                notification.CancellationReason,
                notification.OccurredAt),
            cancellationToken);
}
