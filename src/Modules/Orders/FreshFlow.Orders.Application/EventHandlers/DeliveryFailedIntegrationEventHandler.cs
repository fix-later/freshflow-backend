using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

/// <summary>
/// C2 — a failed delivery is final: cancel the order and refund immediately, no retry.
/// Does NOT release stock: by the time an order reaches Delivering its reservation was already
/// settled at procurement handover (<c>ProcurementBatchHandedOffIntegrationEventHandler</c> calls
/// <c>ConsumeStockAsync</c>, which decrements both <c>CurrentQuantity</c> and
/// <c>ReservedQuantity</c>, plus <c>ReleaseStockAsync</c> for any shortfall). There is no
/// outstanding reservation left to release — calling <c>ReleaseStockAsync</c> here would match
/// zero rows and fail the transaction.
/// Known limitation (C3, out of scope): <see cref="ICreditService.RefundAsync"/> caps the refund
/// at the account's current outstanding balance, so a restaurant that already settled this
/// month's statement gets a shrunk or impossible refund. Not a bug in this handler.
/// </summary>
internal sealed class DeliveryFailedIntegrationEventHandler(
    IOrderRepository orders,
    ICreditService creditService,
    ILogger<DeliveryFailedIntegrationEventHandler> logger)
    : INotificationHandler<DeliveryFailedIntegrationEvent>
{
    public async Task Handle(DeliveryFailedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var transaction = await orders.ExecuteInSerializableTransactionAsync(async ct =>
            {
                var order = await orders.FindByIdAsync(notification.OrderId, ct);
                if (order is null)
                {
                    logger.LogWarning(
                        "Skipping failed-delivery cancellation because OrderId={OrderId} was not found for DeliveryId={DeliveryId}.",
                        notification.OrderId,
                        notification.DeliveryId);
                    return Result.Success();
                }

                if (order.Status == OrderStatus.Cancelled)
                    return Result.Success();

                var cancellation = order.CancelForFailedDelivery(notification.Reason);
                if (cancellation.IsFailure)
                    return cancellation;

                var refund = await creditService.RefundAsync(
                    order.RestaurantId, order.Id, order.TotalAmount, "Delivery failed", ct);

                return refund.IsFailure ? Result.Failure(refund.Error) : Result.Success();
            }, cancellationToken);

            if (transaction.IsFailure)
                logger.LogWarning(
                    "Skipping failed-delivery cancellation for OrderId={OrderId}, DeliveryId={DeliveryId}: {ErrorCode}.",
                    notification.OrderId,
                    notification.DeliveryId,
                    transaction.Error.Code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to cancel OrderId={OrderId} for failed DeliveryId={DeliveryId}.",
                notification.OrderId,
                notification.DeliveryId);
        }
    }
}
