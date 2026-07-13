using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

internal sealed class DeliveryCompletedIntegrationEventHandler(
    IOrderRepository orders,
    ILogger<DeliveryCompletedIntegrationEventHandler> logger)
    : INotificationHandler<DeliveryCompletedIntegrationEvent>
{
    public async Task Handle(
        DeliveryCompletedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await orders.FindByIdAsync(notification.OrderId, cancellationToken);
            if (order is null)
            {
                logger.LogWarning(
                    "Skipping delivery completion because OrderId={OrderId} was not found for RouteId={RouteId}.",
                    notification.OrderId,
                    notification.RouteId);
                return;
            }

            var result = order.AdvanceStatus(OrderStatus.Delivered);
            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Skipping delivery completion for OrderId={OrderId} because transition failed with {ErrorCode}.",
                    notification.OrderId,
                    result.Error.Code);
                return;
            }

            await orders.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to handle delivery completion for OrderId={OrderId}, RouteId={RouteId}.",
                notification.OrderId,
                notification.RouteId);
        }
    }
}
