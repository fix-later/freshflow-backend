using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

internal sealed class HubDiscrepancyRecordedIntegrationEventHandler(
    IOrderRepository orders,
    ICreditService creditService,
    IPublisher publisher,
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
                    "Skipping hub discrepancy refund because OrderId={OrderId} was not found.",
                    notification.OrderId);
                return;
            }

            var item = order.Items.FirstOrDefault(i => i.Id == notification.OrderItemId);
            if (item is null)
            {
                logger.LogWarning(
                    "Skipping hub discrepancy refund because OrderItemId={OrderItemId} was not found.",
                    notification.OrderItemId);
                return;
            }

            if (item.LockedUnitPrice is null)
            {
                logger.LogWarning(
                    "Skipping hub discrepancy refund because OrderItemId={OrderItemId} has no locked unit price.",
                    notification.OrderItemId);
                return;
            }

            // AUDIT-2026-08-23 C3: no more clamp to the account's current balance — refunding
            // past zero is now valid (OutstandingBalance goes negative; FreshFlow owes the
            // restaurant). The per-order refundable-amount cap inside RefundAsync is the guard.
            var desiredRefund = notification.AffectedQuantity * item.LockedUnitPrice.Value;

            // ponytail: no dedupe table in MVP; DiscrepancyId in the credit note is the trace key if retries become real.
            var refund = await creditService.RefundAsync(
                order.RestaurantId,
                order.Id,
                desiredRefund,
                $"Hub discrepancy {notification.DiscrepancyId} refund for order item {notification.OrderItemId}.",
                cancellationToken);

            if (refund.IsFailure)
            {
                logger.LogWarning(
                    "Hub discrepancy refund failed for DiscrepancyId={DiscrepancyId} with {ErrorCode}.",
                    notification.DiscrepancyId,
                    refund.Error.Code);
                return;
            }

            await publisher.Publish(
                new RestaurantRefundIssuedIntegrationEvent(
                    order.RestaurantId,
                    order.Id,
                    item.ProductNameSnapshot,
                    notification.AffectedQuantity,
                    desiredRefund,
                    DateTime.UtcNow),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to handle hub discrepancy refund for DiscrepancyId={DiscrepancyId}.",
                notification.DiscrepancyId);
        }
    }
}
