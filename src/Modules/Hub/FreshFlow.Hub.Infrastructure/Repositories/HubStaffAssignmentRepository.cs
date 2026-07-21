using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Infrastructure.Repositories;

internal sealed class HubStaffAssignmentRepository(AppDbContext db)
    : IHubStaffAssignmentRepository
{
    public async Task<IReadOnlyList<Guid>> GetUserIdsByHubAsync(
        Guid hubId,
        CancellationToken ct) =>
        await db.Set<HubStaffAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.HubId == hubId)
            .OrderBy(assignment => assignment.UserId)
            .Select(assignment => assignment.UserId)
            .ToListAsync(ct);

    public Task<bool> IsAssignedAsync(Guid hubId, Guid userId, CancellationToken ct) =>
        db.Set<HubStaffAssignment>()
            .AsNoTracking()
            .AnyAsync(
                assignment => assignment.HubId == hubId && assignment.UserId == userId,
                ct);

    public async Task<IReadOnlyList<HubEntity>> GetActiveHubsByUserAsync(
        Guid userId,
        CancellationToken ct) =>
        await db.Set<HubStaffAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId)
            .Join(
                db.Set<HubEntity>().AsNoTracking().Where(hub =>
                    hub.IsActive && hub.DeletedAt == null),
                assignment => assignment.HubId,
                hub => hub.Id,
                (_, hub) => hub)
            .OrderByDescending(hub => hub.CreatedAt)
            .ThenByDescending(hub => hub.Id)
            .ToListAsync(ct);

    public async Task ReplaceAsync(
        Guid hubId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM hubs WHERE id = {hubId} FOR UPDATE",
            ct);

        var existing = await db.Set<HubStaffAssignment>()
            .Where(assignment => assignment.HubId == hubId)
            .ToListAsync(ct);
        var requested = userIds.ToHashSet();
        var existingUserIds = existing.Select(assignment => assignment.UserId).ToHashSet();

        db.Set<HubStaffAssignment>()
            .RemoveRange(existing.Where(assignment => !requested.Contains(assignment.UserId)));

        await db.Set<HubStaffAssignment>().AddRangeAsync(
            requested
                .Where(userId => !existingUserIds.Contains(userId))
                .Select(userId => new HubStaffAssignment(hubId, userId)),
            ct);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
