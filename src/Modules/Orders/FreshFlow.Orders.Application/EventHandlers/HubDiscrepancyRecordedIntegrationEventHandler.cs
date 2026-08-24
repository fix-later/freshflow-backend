using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

/// <summary>
/// AUDIT-2026-08-23 C4: routes a hub-flagged discrepancy through
/// <see cref="Orders.Domain.Entities.Order.RecordActualQuantity"/> instead of refunding directly,
/// so the refund and the fulfilled-quantity the VAT invoice reads (<c>ActualQuantity</c>) move
/// together. The refund itself happens via <see cref="Orders.Domain.Events.OrderProcurementShortfallDomainEvent"/>
/// → <see cref="OrderProcurementShortfallDomainEventHandler"/>, which already refunds goods + VAT
/// and publishes <see cref="RestaurantRefundIssuedIntegrationEvent"/>.
/// </summary>
internal sealed class HubDiscrepancyRecordedIntegrationEventHandler(
    IOrderRepository orders,
    ILogger<HubDiscrepancyRecordedIntegrationEventHandler> logger)
    : INotificationHandler<HubDiscrepancyRecordedIntegrationEvent>
{
    public async Task Handle(
        HubDiscrepancyRecordedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await orders.FindByIdAsync(notification.OrderId, cancellationToken);
            if (order is null)
            {
                logger.LogWarning(
                    "Skipping hub discrepancy adjustment because OrderId={OrderId} was not found.",
                    notification.OrderId);
                return;
            }

            var item = order.Items.FirstOrDefault(i => i.Id == notification.OrderItemId);
            if (item is null)
            {
                logger.LogWarning(
                    "Skipping hub discrepancy adjustment because OrderItemId={OrderItemId} was not found.",
                    notification.OrderItemId);
                return;
            }

            // ponytail: no dedupe table in MVP; DiscrepancyId is the trace key if retries become real.
            var newActual = (item.ActualQuantity ?? item.Quantity) - notification.AffectedQuantity;
            var adjustment = order.RecordActualQuantity(notification.OrderItemId, newActual);
            if (adjustment.IsFailure)
            {
                logger.LogWarning(
                    "Hub discrepancy adjustment failed for DiscrepancyId={DiscrepancyId} with {ErrorCode}.",
                    notification.DiscrepancyId,
                    adjustment.Error.Code);
                return;
            }

            orders.Track(order);
            await orders.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to handle hub discrepancy adjustment for DiscrepancyId={DiscrepancyId}.",
                notification.DiscrepancyId);
        }
    }
}
