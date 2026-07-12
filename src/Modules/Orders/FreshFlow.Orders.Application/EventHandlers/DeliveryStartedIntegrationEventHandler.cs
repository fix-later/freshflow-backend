using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

internal sealed class DeliveryStartedIntegrationEventHandler(
    IOrderRepository orders,
    ILogger<DeliveryStartedIntegrationEventHandler> logger)
    : INotificationHandler<DeliveryStartedIntegrationEvent>
{
    public async Task Handle(
        DeliveryStartedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        // ponytail: best-effort cross-module side effect; add an outbox/dedupe table if retries need strict atomicity.
        foreach (var orderId in notification.OrderIds)
        {
            try
            {
                var order = await orders.FindByIdAsync(orderId, cancellationToken);
                if (order is null)
                {
                    logger.LogWarning(
                        "Skipping delivery start because OrderId={OrderId} was not found for RouteId={RouteId}.",
                        orderId,
                        notification.RouteId);
                    continue;
                }

                var result = order.AdvanceStatus(OrderStatus.Delivering);
                if (result.IsFailure)
                {
                    logger.LogWarning(
                        "Skipping delivery start for OrderId={OrderId} because transition failed with {ErrorCode}.",
                        orderId,
                        result.Error.Code);
                    continue;
                }

                await orders.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Failed to handle delivery start for OrderId={OrderId}, RouteId={RouteId}.",
                    orderId,
                    notification.RouteId);
            }
        }
    }
}
