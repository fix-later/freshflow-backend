using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class HubDiscrepancyRecordedAuditLogHandler(IAuditLogWriter writer)
    : INotificationHandler<HubDiscrepancyRecordedIntegrationEvent>
{
    public Task Handle(HubDiscrepancyRecordedIntegrationEvent notification, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: null,
            action: "hub_discrepancy_recorded",
            entityType: "hub_discrepancy",
            entityId: notification.DiscrepancyId,
            details: JsonSerializer.Serialize(new
            {
                hubId = notification.HubId,
                orderId = notification.OrderId,
                affectedQuantity = notification.AffectedQuantity,
                conditionStatus = notification.ConditionStatus,
            }),
            occurredAt: notification.OccurredAt,
            ct: cancellationToken);
}
