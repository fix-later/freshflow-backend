using FreshFlow.Contracts;
using FreshFlow.Procurement.Domain.Events;
using MediatR;

namespace FreshFlow.Procurement.Application.EventHandlers;

internal sealed class ProcurementBatchBuiltDomainEventHandler(IPublisher publisher)
    : INotificationHandler<ProcurementBatchBuiltDomainEvent>
{
    public Task Handle(
        ProcurementBatchBuiltDomainEvent notification,
        CancellationToken cancellationToken) =>
        publisher.Publish(
            new ProcurementBatchBuiltIntegrationEvent(
                notification.BatchId,
                notification.MarketId,
                notification.BatchDate,
                notification.CoveredOrderIds),
            cancellationToken);
}
