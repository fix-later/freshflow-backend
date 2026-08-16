using FreshFlow.Contracts;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Procurement.Application.EventHandlers;

/// <summary>
/// Flips a procurement session to Completed once every order it covers has settled
/// (Delivered or Cancelled). A failure here must never break the delivery flow, so all
/// exceptions are swallowed after logging.
/// </summary>
internal sealed class DeliveryCompletedIntegrationEventHandler(
    IProcurementBatchRepository batches,
    IConfirmedOrderReader orders,
    ILogger<DeliveryCompletedIntegrationEventHandler> logger)
    : INotificationHandler<DeliveryCompletedIntegrationEvent>
{
    public async Task Handle(DeliveryCompletedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var batch = await batches.FindByOrderIdAsync(notification.OrderId, cancellationToken);
            if (batch is null || batch.Status != ProcurementBatchStatus.HandedOff)
                return;

            var orderIds = batch.Orders.Select(link => link.OrderId).Distinct().ToArray();
            var statuses = await orders.ReadStatusesAsync(orderIds, cancellationToken);
            var allSettled = orderIds.All(orderId =>
                ProcurementOrderSettlement.IsSettled(statuses.GetValueOrDefault(orderId)));
            if (!allSettled)
                return;

            var result = batch.MarkCompleted(notification.OccurredAt);
            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Skipping batch completion for BatchId={BatchId} because transition failed with {ErrorCode}.",
                    batch.Id,
                    result.Error.Code);
                return;
            }

            await batches.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to handle delivery completion for procurement batching, OrderId={OrderId}, RouteId={RouteId}.",
                notification.OrderId,
                notification.RouteId);
        }
    }
}
