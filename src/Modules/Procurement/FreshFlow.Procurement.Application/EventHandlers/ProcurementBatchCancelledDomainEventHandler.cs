using FreshFlow.Contracts;
using FreshFlow.Procurement.Domain.Events;
using MediatR;

namespace FreshFlow.Procurement.Application.EventHandlers;

internal sealed class ProcurementBatchCancelledDomainEventHandler(IPublisher publisher)
    : INotificationHandler<ProcurementBatchCancelledDomainEvent>
{
    public Task Handle(
        ProcurementBatchCancelledDomainEvent notification,
        CancellationToken cancellationToken) =>
        publisher.Publish(
            new ProcurementBatchCancelledIntegrationEvent(
                notification.BatchId,
                notification.MarketId,
                notification.Reason,
                notification.CancelledAt,
                notification.CoveredOrderIds),
            cancellationToken);
}
