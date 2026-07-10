using FreshFlow.Contracts;
using FreshFlow.Hub.Domain.Events;
using MediatR;

namespace FreshFlow.Hub.Application.EventHandlers;

internal sealed class HubDiscrepancyRecordedDomainEventHandler(IPublisher publisher)
    : INotificationHandler<HubDiscrepancyRecordedDomainEvent>
{
    public Task Handle(HubDiscrepancyRecordedDomainEvent notification, CancellationToken cancellationToken) =>
        publisher.Publish(
            new HubDiscrepancyRecordedIntegrationEvent(
                notification.DiscrepancyId,
                notification.HubId,
                notification.InboundEventId,
                notification.OrderId,
                notification.OrderItemId,
                notification.AffectedQuantity,
                notification.ConditionStatus,
                notification.OccurredAt),
            cancellationToken);
}
