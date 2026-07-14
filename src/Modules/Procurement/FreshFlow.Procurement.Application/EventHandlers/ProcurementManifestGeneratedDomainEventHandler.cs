using FreshFlow.Contracts;
using FreshFlow.Procurement.Domain.Events;
using MediatR;

namespace FreshFlow.Procurement.Application.EventHandlers;

internal sealed class ProcurementManifestGeneratedDomainEventHandler(IPublisher publisher)
    : INotificationHandler<ProcurementManifestGeneratedDomainEvent>
{
    public Task Handle(
        ProcurementManifestGeneratedDomainEvent notification,
        CancellationToken cancellationToken) =>
        publisher.Publish(
            new ProcurementManifestGeneratedIntegrationEvent(
                notification.BatchId,
                notification.MarketId,
                notification.BatchDate,
                notification.ManifestedAt),
            cancellationToken);
}
