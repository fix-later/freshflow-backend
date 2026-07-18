using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

internal sealed class ProcurementBatchCancelledIntegrationEventHandler(
    IOrderRepository orders,
    ILogger<ProcurementBatchCancelledIntegrationEventHandler> logger)
    : INotificationHandler<ProcurementBatchCancelledIntegrationEvent>
{
    public async Task Handle(
        ProcurementBatchCancelledIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        foreach (var orderId in notification.CoveredOrderIds.Distinct())
        {
            try
            {
                var order = await orders.FindByIdAsync(orderId, cancellationToken);
                if (order is null)
                {
                    logger.LogWarning(
                        "Skipping batch cancellation because OrderId={OrderId} was not found for BatchId={BatchId}.",
                        orderId,
                        notification.BatchId);
                    continue;
                }

                var cancellation = order.CancelWithSession(notification.Reason);
                if (cancellation.IsFailure)
                {
                    logger.LogWarning(
                        "Skipping batch cancellation for OrderId={OrderId}, BatchId={BatchId}: {ErrorCode}.",
                        orderId,
                        notification.BatchId,
                        cancellation.Error.Code);
                    continue;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Failed to cancel OrderId={OrderId} for cancelled procurement BatchId={BatchId}.",
                    orderId,
                    notification.BatchId);
            }
        }

        // One save for the whole batch: a per-order save leaves the failed order still
        // tracked, so every later iteration retries it and cascades. Let this throw so
        // the caller retries the handler as a unit.
        await orders.SaveChangesAsync(cancellationToken);
    }
}
