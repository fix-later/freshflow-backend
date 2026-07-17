using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class ProcurementBatchHandedOffAuditLogHandler(IAuditLogWriter writer)
    : INotificationHandler<ProcurementBatchHandedOffIntegrationEvent>
{
    public Task Handle(
        ProcurementBatchHandedOffIntegrationEvent notification,
        CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: null,
            action: "procurement_batch_handed_off",
            entityType: "procurement_batch",
            entityId: notification.BatchId,
            details: JsonSerializer.Serialize(new
            {
                marketId = notification.MarketId,
                hubId = notification.HubId,
                coveredOrderCount = notification.CoveredOrderIds.Count,
            }),
            occurredAt: notification.HandedOffAt,
            ct: cancellationToken);
}
