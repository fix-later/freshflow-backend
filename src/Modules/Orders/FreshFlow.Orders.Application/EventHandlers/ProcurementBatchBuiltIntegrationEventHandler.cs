using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

internal sealed class ProcurementBatchBuiltIntegrationEventHandler(
    IOrderRepository orders,
    ILogger<ProcurementBatchBuiltIntegrationEventHandler> logger)
    : INotificationHandler<ProcurementBatchBuiltIntegrationEvent>
{
    public async Task Handle(
        ProcurementBatchBuiltIntegrationEvent notification,
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
                        "Skipping procurement batch propagation because OrderId={OrderId} was not found for BatchId={BatchId}.",
                        orderId,
                        notification.BatchId);
                    continue;
                }

                var transition = order.AdvanceStatus(OrderStatus.Batched);
                if (transition.IsFailure)
                {
                    logger.LogWarning(
                        "Skipping procurement batch propagation for OrderId={OrderId}, BatchId={BatchId}: {ErrorCode}.",
                        orderId,
                        notification.BatchId,
                        transition.Error.Code);
                    continue;
                }

                await orders.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Failed to propagate procurement BatchId={BatchId} to OrderId={OrderId}.",
                    notification.BatchId,
                    orderId);
            }
        }
    }
}
