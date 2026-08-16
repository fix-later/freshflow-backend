using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class ProcurementBatchBuiltAuditLogHandler(
    IAuditLogWriter writer,
    TimeProvider timeProvider)
    : INotificationHandler<ProcurementBatchBuiltIntegrationEvent>
{
    public Task Handle(ProcurementBatchBuiltIntegrationEvent notification, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: null,
            action: "procurement_batch_built",
            entityType: "procurement_batch",
            entityId: notification.BatchId,
            details: JsonSerializer.Serialize(new
            {
                marketId = notification.MarketId,
                batchDate = notification.BatchDate,
                coveredOrderCount = notification.CoveredOrderIds.Count,
            }),
            occurredAt: timeProvider.GetUtcNow().UtcDateTime,
            ct: cancellationToken);
}
