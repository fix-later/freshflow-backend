using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.Repositories;

internal sealed class HubSortingProgressRepository(AppDbContext db) : IHubSortingProgressRepository
{
    public Task<HubSortingProgress?> FindByHubDateAndOrderItemAsync(
        Guid hubId, DateOnly serviceDate, Guid orderItemId, CancellationToken ct) =>
        db.Set<HubSortingProgress>()
            .FirstOrDefaultAsync(p =>
                p.HubId == hubId &&
                p.ServiceDate == serviceDate &&
                p.OrderItemId == orderItemId &&
                p.DeletedAt == null, ct);

    public async Task<IReadOnlyList<HubSortingProgress>> ListByHubAndDateAsync(
        Guid hubId, DateOnly serviceDate, CancellationToken ct) =>
        await db.Set<HubSortingProgress>()
            .AsNoTracking()
            .Where(p => p.HubId == hubId && p.ServiceDate == serviceDate && p.DeletedAt == null)
            .ToListAsync(ct);

    public async Task AddAsync(HubSortingProgress progress, CancellationToken ct) =>
        await db.Set<HubSortingProgress>().AddAsync(progress, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
