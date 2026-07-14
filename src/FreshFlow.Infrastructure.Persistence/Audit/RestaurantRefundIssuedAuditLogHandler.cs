using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class RestaurantRefundIssuedAuditLogHandler(IAuditLogWriter writer)
    : INotificationHandler<RestaurantRefundIssuedIntegrationEvent>
{
    public Task Handle(RestaurantRefundIssuedIntegrationEvent notification, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: null,
            action: "restaurant_refund_issued",
            entityType: "restaurant",
            entityId: notification.RestaurantId,
            details: JsonSerializer.Serialize(new
            {
                orderId = notification.OrderId,
                orderItemName = notification.OrderItemName,
                affectedQuantity = notification.AffectedQuantity,
                refundAmount = notification.RefundAmount,
            }),
            occurredAt: notification.OccurredAt,
            ct: cancellationToken);
}
