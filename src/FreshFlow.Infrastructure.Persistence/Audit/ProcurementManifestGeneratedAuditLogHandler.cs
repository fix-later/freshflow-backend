using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class ProcurementManifestGeneratedAuditLogHandler(IAuditLogWriter writer)
    : INotificationHandler<ProcurementManifestGeneratedIntegrationEvent>
{
    public Task Handle(
        ProcurementManifestGeneratedIntegrationEvent notification,
        CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: null,
            action: "procurement_manifest_generated",
            entityType: "procurement_batch",
            entityId: notification.BatchId,
            details: JsonSerializer.Serialize(new
            {
                marketId = notification.MarketId,
                batchDate = notification.BatchDate,
            }),
            occurredAt: notification.ManifestedAt,
            ct: cancellationToken);
}
