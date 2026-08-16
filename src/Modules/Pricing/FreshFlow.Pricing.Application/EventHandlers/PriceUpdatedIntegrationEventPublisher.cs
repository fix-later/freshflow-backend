using FreshFlow.Contracts;
using FreshFlow.Pricing.Domain.Events;
using MediatR;

namespace FreshFlow.Pricing.Application.EventHandlers;

/// <summary>
/// Translates <see cref="PriceUpdatedDomainEvent"/> into <see cref="PriceUpdatedIntegrationEvent"/>
/// for cross-module consumption (Admin audit log, SCRUM-359a).
/// </summary>
internal sealed class PriceUpdatedIntegrationEventPublisher(IPublisher publisher)
    : INotificationHandler<PriceUpdatedDomainEvent>
{
    public Task Handle(PriceUpdatedDomainEvent notification, CancellationToken cancellationToken) =>
        publisher.Publish(
            new PriceUpdatedIntegrationEvent(
                notification.MarketProductId,
                notification.MarketId,
                notification.ProductId,
                notification.OldPrice,
                notification.NewPrice,
                notification.CurrentQuantity,
                notification.UpdatedBy,
                notification.OccurredAt),
            cancellationToken);
}
