namespace FreshFlow.SharedKernel.Application;

/// <summary>
/// Appends a row to the system-wide audit log (SCRUM-359b). Write-only — never throws for
/// a failed write; implementations log and swallow so audit failures never break the
/// originating business operation.
/// </summary>
public interface IAuditLogWriter
{
    public Task WriteAsync(
        Guid? actorId,
        string action,
        string entityType,
        Guid entityId,
        string? details,
        DateTime occurredAt,
        CancellationToken ct);
}
