using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class OrderCancelledAuditLogHandler(IAuditLogWriter writer)
    : INotificationHandler<OrderCancelledIntegrationEvent>
{
    public Task Handle(OrderCancelledIntegrationEvent notification, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: null,
            action: "order_cancelled",
            entityType: "order",
            entityId: notification.OrderId,
            details: JsonSerializer.Serialize(new
            {
                restaurantId = notification.RestaurantId,
                cancellationReason = notification.CancellationReason,
            }),
            occurredAt: notification.OccurredAt,
            ct: cancellationToken);
}
