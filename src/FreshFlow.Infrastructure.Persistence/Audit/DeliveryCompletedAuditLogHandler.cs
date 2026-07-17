using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class DeliveryCompletedAuditLogHandler(IAuditLogWriter writer)
    : INotificationHandler<DeliveryCompletedIntegrationEvent>
{
    public Task Handle(DeliveryCompletedIntegrationEvent notification, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: null,
            action: "delivery_completed",
            entityType: "order",
            entityId: notification.OrderId,
            details: JsonSerializer.Serialize(new
            {
                routeId = notification.RouteId,
                actualArrivalAt = notification.ActualArrivalAt,
            }),
            occurredAt: notification.OccurredAt,
            ct: cancellationToken);
}
