using FreshFlow.Infrastructure.Persistence.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class GetAuditLogsQueryHandler(AppDbContext db)
    : IRequestHandler<GetAuditLogsQuery, Result<AuditLogPageDto>>
{
    public async Task<Result<AuditLogPageDto>> Handle(GetAuditLogsQuery request, CancellationToken ct)
    {
        var query = db.Set<AuditLog>().AsNoTracking().AsQueryable();

        if (request.ActorId.HasValue)
            query = query.Where(a => a.ActorId == request.ActorId.Value);
        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(a => a.Action == request.Action);
        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(a => a.EntityType == request.EntityType);
        if (request.From.HasValue)
            query = query.Where(a => a.OccurredAt >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(a => a.OccurredAt <= request.To.Value);

        var total = await query.CountAsync(ct);
        var data = await query
            .OrderByDescending(a => a.OccurredAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogDto(
                a.Id, a.ActorId, a.Action, a.EntityType, a.EntityId, a.Details, a.OccurredAt, a.CreatedAt))
            .ToListAsync(ct);

        return Result<AuditLogPageDto>.Success(new AuditLogPageDto(data, request.Page, request.PageSize, total));
    }
}
