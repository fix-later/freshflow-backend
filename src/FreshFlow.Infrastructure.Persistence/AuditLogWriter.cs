using FreshFlow.Infrastructure.Persistence.Entities;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Infrastructure.Persistence;

internal sealed class AuditLogWriter(AppDbContext db, ILogger<AuditLogWriter> logger) : IAuditLogWriter
{
    public async Task WriteAsync(
        Guid? actorId,
        string action,
        string entityType,
        Guid entityId,
        string? details,
        DateTime occurredAt,
        CancellationToken ct)
    {
        try
        {
            db.Set<AuditLog>().Add(new AuditLog(
                Guid.NewGuid(), actorId, action, entityType, entityId, details, occurredAt, DateTime.UtcNow));
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Intentionally swallowed — an audit-write failure must not fail the originating
            // business operation (same contract as PriceUpdatedDomainEventHandler's broadcast).
            logger.LogError(ex, "Failed to write audit log for Action={Action} EntityId={EntityId}", action, entityId);
        }
    }
}
