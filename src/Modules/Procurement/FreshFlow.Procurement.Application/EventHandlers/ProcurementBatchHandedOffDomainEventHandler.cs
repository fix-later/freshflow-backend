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
        publisher.Publish(Map(notification), cancellationToken);

    internal static ProcurementBatchHandedOffIntegrationEvent Map(
        ProcurementBatchHandedOffDomainEvent notification) =>
        new(
            notification.BatchId,
            notification.MarketId,
            notification.HubId,
            notification.HandedOffAt,
            notification.CoveredOrderIds,
            notification.HandedOffByUserId,
            notification.PurchasedLines?
                .Select(line => new ProcurementPurchaseActual(
                    line.MarketProductId,
                    line.ActualQuantity,
                    line.ActualUnitPrice))
                .ToList()
                .AsReadOnly());
}
