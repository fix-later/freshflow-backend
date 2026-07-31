using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

internal sealed class ProcurementBatchCancelledIntegrationEventHandler(
    IOrderRepository orders,
    ICreditService creditService,
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
                var transaction = await orders.ExecuteInSerializableTransactionAsync(async ct =>
                {
                    var order = await orders.FindByIdAsync(orderId, ct);
                    if (order is null)
                    {
                        logger.LogWarning(
                            "Skipping batch cancellation because OrderId={OrderId} was not found for BatchId={BatchId}.",
                            orderId,
                            notification.BatchId);
                        return Result.Success();
                    }

                    if (order.Status == OrderStatus.Cancelled)
                        return Result.Success();

                    var cancellation = order.CancelWithSession(notification.Reason);
                    if (cancellation.IsFailure)
                        return cancellation;

                    var reservations = order.Items
                        .GroupBy(item => item.MarketProductId)
                        .Select(group => new StockReservation(group.Key, group.Sum(item => item.Quantity)))
                        .OrderBy(reservation => reservation.MarketProductId)
                        .ToArray();

                    if (!await orders.ReleaseStockAsync(reservations, ct))
                        return Result.Failure(Error.Conflict(
                            "STOCK_RESERVATION_CONFLICT",
                            "The order stock reservation could not be released."));

                    var refund = await creditService.RefundAsync(
                        order.RestaurantId,
                        order.Id,
                        order.TotalAmount,
                        "Procurement batch cancelled",
                        ct);

                    return refund.IsFailure ? Result.Failure(refund.Error) : Result.Success();
                }, cancellationToken);

                if (transaction.IsFailure)
                    logger.LogWarning(
                        "Skipping batch cancellation for OrderId={OrderId}, BatchId={BatchId}: {ErrorCode}.",
                        orderId,
                        notification.BatchId,
                        transaction.Error.Code);
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
    }
}
