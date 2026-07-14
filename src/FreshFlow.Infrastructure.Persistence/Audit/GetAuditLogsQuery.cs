using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Infrastructure.Persistence.Audit;

public sealed record GetAuditLogsQuery(
    Guid? ActorId,
    string? Action,
    string? EntityType,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20) : IQuery<AuditLogPageDto>;

public sealed record AuditLogDto(
    Guid Id,
    Guid? ActorId,
    string Action,
    string EntityType,
    Guid EntityId,
    string? Details,
    DateTime OccurredAt,
    DateTime CreatedAt);

public sealed record AuditLogPageDto(IReadOnlyList<AuditLogDto> Data, int Page, int PageSize, int Total);
