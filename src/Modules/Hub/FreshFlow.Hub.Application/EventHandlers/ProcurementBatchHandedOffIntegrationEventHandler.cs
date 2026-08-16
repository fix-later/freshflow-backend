using FreshFlow.Contracts;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Hub.Application.EventHandlers;

internal sealed class ProcurementBatchHandedOffIntegrationEventHandler(
    IHubProcurementPlanReader procurement,
    IHubInboundRepository inbounds,
    ILogger<ProcurementBatchHandedOffIntegrationEventHandler> logger)
    : INotificationHandler<ProcurementBatchHandedOffIntegrationEvent>
{
    public async Task Handle(
        ProcurementBatchHandedOffIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        if (notification.HubId is null)
        {
            logger.LogInformation(
                "Skipping hub inbound auto-creation for BatchId={BatchId} because it has no resolved hub.",
                notification.BatchId);
            return;
        }

        var hubId = notification.HubId.Value;

        try
        {
            var planItems = await procurement.ReadBatchItemsAsync(notification.BatchId, cancellationToken);
            var items = planItems
                .Where(item => item.ActualQuantity is > 0)
                .Select(item => new HubInboundItem(
                    item.MarketProductId,
                    ProductId: null,
                    QuantityKg: item.ActualQuantity!.Value,
                    ProductName: item.ProductName))
                .ToList()
                .AsReadOnly();

            if (items.Count == 0)
            {
                logger.LogInformation(
                    "Skipping hub inbound auto-creation for BatchId={BatchId} because no items have a purchased quantity.",
                    notification.BatchId);
                return;
            }

            if (await inbounds.DeliveryScheduleExistsAsync(hubId, notification.BatchId, cancellationToken))
                return;

            var inbound = HubInboundEvent.Record(
                hubId,
                sourceMarketId: notification.MarketId,
                deliveryRouteId: null,
                deliveryScheduleId: notification.BatchId,
                items,
                arrivedAt: notification.HandedOffAt,
                recordedBy: notification.HandedOffByUserId,
                hubStaffUserId: null);

            await inbounds.AddAsync(inbound, cancellationToken);

            try
            {
                await inbounds.SaveChangesAsync(cancellationToken);
            }
            catch (HubConcurrencyException)
            {
                // Already recorded by a concurrent handover — not an error.
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to auto-create hub inbound event for BatchId={BatchId}, HubId={HubId}.",
                notification.BatchId,
                hubId);
        }
    }
}
