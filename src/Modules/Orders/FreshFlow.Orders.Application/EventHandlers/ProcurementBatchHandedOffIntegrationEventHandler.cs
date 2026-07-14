using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

internal sealed class ProcurementBatchHandedOffIntegrationEventHandler(
    IOrderRepository orders,
    ILogger<ProcurementBatchHandedOffIntegrationEventHandler> logger)
    : INotificationHandler<ProcurementBatchHandedOffIntegrationEvent>
{
    public async Task Handle(
        ProcurementBatchHandedOffIntegrationEvent notification,
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
                        "Skipping procurement handover propagation because OrderId={OrderId} was not found for BatchId={BatchId}.",
                        orderId,
                        notification.BatchId);
                    continue;
                }

                if (order.Status == OrderStatus.Batched)
                {
                    var pickup = order.AdvanceStatus(OrderStatus.PickedUp);
                    if (pickup.IsFailure)
                        LogTransitionFailure("pickup", orderId, notification.BatchId, pickup.Error.Code);
                }

                if (order.Status == OrderStatus.PickedUp)
                {
                    var atHub = order.AdvanceStatus(OrderStatus.AtHub);
                    if (atHub.IsFailure)
                        LogTransitionFailure("hub", orderId, notification.BatchId, atHub.Error.Code);
                }

                await orders.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Failed to propagate procurement handover BatchId={BatchId} to OrderId={OrderId}.",
                    notification.BatchId,
                    orderId);
            }
        }
    }

    private void LogTransitionFailure(
        string hop,
        Guid orderId,
        Guid batchId,
        string errorCode)
    {
        logger.LogWarning(
            "Failed procurement handover {Hop} hop for OrderId={OrderId}, BatchId={BatchId}: {ErrorCode}.",
            hop,
            orderId,
            batchId,
            errorCode);
    }
}
