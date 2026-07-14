using FreshFlow.Contracts;
using FreshFlow.Procurement.Domain.Events;
using MediatR;

namespace FreshFlow.Procurement.Application.EventHandlers;

internal sealed class ProcurementBatchHandedOffDomainEventHandler(IPublisher publisher)
    : INotificationHandler<ProcurementBatchHandedOffDomainEvent>
{
    public Task Handle(
        ProcurementBatchHandedOffDomainEvent notification,
        CancellationToken cancellationToken) =>
        publisher.Publish(
            new ProcurementBatchHandedOffIntegrationEvent(
                notification.BatchId,
                notification.MarketId,
                notification.HubId,
                notification.HandedOffAt,
                notification.CoveredOrderIds),
            cancellationToken);
}
