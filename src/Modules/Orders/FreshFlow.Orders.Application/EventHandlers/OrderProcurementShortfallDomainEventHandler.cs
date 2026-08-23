using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

/// <summary>
/// C1 — refunds a procurement shortfall (the agent bought less than the restaurant ordered) to
/// the restaurant's credit account. <see cref="OrderProcurementShortfallDomainEvent"/> already
/// carries the computed refund amount (goods + proportional VAT), so this handler is a thin
/// ledger call, not a recomputation.
///
/// Known limitations, out of scope here:
/// - C3: <see cref="ICreditService.RefundAsync"/> caps the refund at the account's current
///   outstanding balance, so a restaurant that already settled this month's statement gets a
///   shrunk or impossible refund.
/// - C4: the VAT invoice is still issued on the ordered quantity, so a refunded shortfall widens
///   the gap between the invoice and the credit ledger.
/// Neither is a bug in this handler.
/// </summary>
internal sealed class OrderProcurementShortfallDomainEventHandler(
    ICreditService creditService,
    IPublisher publisher,
    ILogger<OrderProcurementShortfallDomainEventHandler> logger)
    : INotificationHandler<OrderProcurementShortfallDomainEvent>
{
    public async Task Handle(OrderProcurementShortfallDomainEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var refund = await creditService.RefundAsync(
                notification.RestaurantId,
                notification.OrderId,
                notification.RefundAmount,
                $"Procurement shortfall: {notification.ShortfallQuantity} short on {notification.ProductName}",
                cancellationToken);

            if (refund.IsFailure)
            {
                logger.LogWarning(
                    "Procurement shortfall refund failed for OrderId={OrderId}, OrderItemId={OrderItemId}: {ErrorCode}.",
                    notification.OrderId,
                    notification.OrderItemId,
                    refund.Error.Code);
                return;
            }

            await publisher.Publish(
                new RestaurantRefundIssuedIntegrationEvent(
                    notification.RestaurantId,
                    notification.OrderId,
                    notification.ProductName,
                    notification.ShortfallQuantity,
                    notification.RefundAmount,
                    DateTime.UtcNow),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to handle procurement shortfall refund for OrderId={OrderId}, OrderItemId={OrderItemId}.",
                notification.OrderId,
                notification.OrderItemId);
        }
    }
}
