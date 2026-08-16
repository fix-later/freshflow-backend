using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class ProcurementAgentAssignedAuditLogHandler(IAuditLogWriter writer)
    : INotificationHandler<ProcurementAgentAssignedIntegrationEvent>
{
    public Task Handle(ProcurementAgentAssignedIntegrationEvent notification, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: null,
            action: "procurement_agent_assigned",
            entityType: "procurement_batch",
            entityId: notification.BatchId,
            details: JsonSerializer.Serialize(new
            {
                marketId = notification.MarketId,
                agentUserId = notification.AgentUserId,
            }),
            occurredAt: notification.AssignedAt,
            ct: cancellationToken);
}
