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
                var transaction = await orders.ExecuteInSerializableTransactionAsync(async ct =>
                {
                    var order = await orders.FindByIdAsync(orderId, ct);
                    if (order is null)
                    {
                        logger.LogWarning(
                            "Skipping procurement handover propagation because OrderId={OrderId} was not found for BatchId={BatchId}.",
                            orderId,
                            notification.BatchId);
                        return FreshFlow.SharedKernel.Application.Result.Success();
                    }

                    if (order.Status == OrderStatus.Batched)
                    {
                        var reservations = order.Items
                            .GroupBy(item => item.MarketProductId)
                            .Select(group => new StockReservation(group.Key, group.Sum(item => item.Quantity)))
                            .OrderBy(reservation => reservation.MarketProductId)
                            .ToArray();

                        if (!await orders.ConsumeStockAsync(reservations, ct))
                            return FreshFlow.SharedKernel.Application.Result.Failure(
                                FreshFlow.SharedKernel.Application.Error.Conflict(
                                    "STOCK_RESERVATION_CONFLICT",
                                    "The order stock reservation could not be consumed."));

                        var pickup = order.AdvanceStatus(OrderStatus.PickedUp);
                        if (pickup.IsFailure)
                            return pickup;
                    }

                    if (order.Status == OrderStatus.PickedUp)
                    {
                        var atHub = order.AdvanceStatus(OrderStatus.AtHub);
                        if (atHub.IsFailure)
                            return atHub;
                    }

                    return FreshFlow.SharedKernel.Application.Result.Success();
                }, cancellationToken);

                if (transaction.IsFailure)
                    LogTransitionFailure("stock/hub", orderId, notification.BatchId, transaction.Error.Code);
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
