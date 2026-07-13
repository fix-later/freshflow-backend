namespace FreshFlow.Infrastructure.Persistence.Entities;

/// <summary>
/// Append-only audit trail row (SCRUM-359b). No module owns this — written by integration-event
/// consumers across modules via <c>IAuditLogWriter</c> — so it lives in the shared Persistence
/// project rather than under a module, same rationale as <see cref="AssistantConversation"/>.
/// No <c>UpdatedAt</c>/<c>DeletedAt</c>: rows are immutable once written (like <c>price_snapshots</c>).
/// </summary>
public sealed class AuditLog
{
    private AuditLog() { } // EF Core

    public AuditLog(
        Guid id,
        Guid? actorId,
        string action,
        string entityType,
        Guid entityId,
        string? details,
        DateTime occurredAt,
        DateTime createdAt)
    {
        Id = id;
        ActorId = actorId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Details = details;
        OccurredAt = occurredAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid? ActorId { get; private set; }
    public string Action { get; private set; } = null!;
    public string EntityType { get; private set; } = null!;
    public Guid EntityId { get; private set; }
    public string? Details { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
