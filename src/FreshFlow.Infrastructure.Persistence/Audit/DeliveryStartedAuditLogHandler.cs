using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class DeliveryStartedAuditLogHandler(IAuditLogWriter writer)
    : INotificationHandler<DeliveryStartedIntegrationEvent>
{
    public Task Handle(DeliveryStartedIntegrationEvent notification, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: null,
            action: "delivery_started",
            entityType: "delivery_route",
            entityId: notification.RouteId,
            details: JsonSerializer.Serialize(new
            {
                orderCount = notification.OrderIds.Count,
            }),
            occurredAt: notification.OccurredAt,
            ct: cancellationToken);
}
