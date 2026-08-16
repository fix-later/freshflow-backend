using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class CreditLimitThresholdReachedAuditLogHandler(IAuditLogWriter writer)
    : INotificationHandler<CreditLimitThresholdReachedIntegrationEvent>
{
    public Task Handle(CreditLimitThresholdReachedIntegrationEvent notification, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: null,
            action: "credit_limit_threshold_reached",
            entityType: "restaurant",
            entityId: notification.RestaurantId,
            details: JsonSerializer.Serialize(new
            {
                level = notification.Level,
                utilization = notification.Utilization,
                outstandingBalance = notification.OutstandingBalance,
                creditLimit = notification.CreditLimit,
            }),
            occurredAt: notification.OccurredAt,
            ct: cancellationToken);
}
