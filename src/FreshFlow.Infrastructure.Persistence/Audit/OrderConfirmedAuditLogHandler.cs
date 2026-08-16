using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class OrderConfirmedAuditLogHandler(IAuditLogWriter writer)
    : INotificationHandler<OrderConfirmedIntegrationEvent>
{
    public Task Handle(OrderConfirmedIntegrationEvent notification, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: null,
            action: "order_confirmed",
            entityType: "order",
            entityId: notification.OrderId,
            details: JsonSerializer.Serialize(new
            {
                restaurantId = notification.RestaurantId,
                totalAmount = notification.TotalAmount,
            }),
            occurredAt: notification.OccurredAt,
            ct: cancellationToken);
}
