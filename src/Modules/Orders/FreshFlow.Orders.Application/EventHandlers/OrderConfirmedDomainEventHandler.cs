using FreshFlow.Contracts;
using FreshFlow.Orders.Domain.Events;
using MediatR;

namespace FreshFlow.Orders.Application.EventHandlers;

/// <summary>
/// Translates <see cref="OrderConfirmedDomainEvent"/> into <see cref="OrderConfirmedIntegrationEvent"/>
/// for cross-module consumption (Logistics creates a delivery record, Notifications notifies the restaurant).
/// </summary>
internal sealed class OrderConfirmedDomainEventHandler(IPublisher publisher)
    : INotificationHandler<OrderConfirmedDomainEvent>
{
    public Task Handle(OrderConfirmedDomainEvent notification, CancellationToken cancellationToken) =>
        publisher.Publish(
            new OrderConfirmedIntegrationEvent(
                notification.OrderId,
                notification.RestaurantId,
                notification.TotalAmount,
                notification.OccurredAt),
            cancellationToken);
}
