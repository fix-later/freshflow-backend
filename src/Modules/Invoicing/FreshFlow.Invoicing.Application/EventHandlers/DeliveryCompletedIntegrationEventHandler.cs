using FreshFlow.Contracts;
using FreshFlow.Invoicing.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Invoicing.Application.EventHandlers;

/// <summary>
/// Issues the VAT invoice for a delivered order. This is the third consumer of
/// DeliveryCompletedIntegrationEvent (alongside Orders and Notifications). A failure here must never
/// break the delivery flow, so all exceptions are swallowed after logging — the retry job recovers.
/// </summary>
internal sealed class DeliveryCompletedIntegrationEventHandler(
    IInvoiceIssuanceService issuance,
    ILogger<DeliveryCompletedIntegrationEventHandler> logger)
    : INotificationHandler<DeliveryCompletedIntegrationEvent>
{
    public async Task Handle(DeliveryCompletedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await issuance.IssueForDeliveredOrderAsync(notification.OrderId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to issue invoice for OrderId={OrderId}, RouteId={RouteId}.",
                notification.OrderId,
                notification.RouteId);
        }
    }
}
