using FreshFlow.Contracts;
using FreshFlow.Procurement.Domain.Events;
using MediatR;

namespace FreshFlow.Procurement.Application.EventHandlers;

internal sealed class ProcurementPurchaseConfirmedDomainEventHandler(IPublisher publisher)
    : INotificationHandler<ProcurementPurchaseConfirmedDomainEvent>
{
    public Task Handle(
        ProcurementPurchaseConfirmedDomainEvent notification,
        CancellationToken cancellationToken) =>
        publisher.Publish(
            new ProcurementPurchaseConfirmedIntegrationEvent(
                notification.BatchId,
                notification.MarketId,
                notification.AgentUserId,
                notification.ActualUnitPrices,
                notification.ConfirmedAt),
            cancellationToken);
}
