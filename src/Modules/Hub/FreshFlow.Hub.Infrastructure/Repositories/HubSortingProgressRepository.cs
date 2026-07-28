using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.Repositories;

internal sealed class HubSortingProgressRepository(AppDbContext db) : IHubSortingProgressRepository
{
    public Task<HubSortingProgress?> FindByRouteAndOrderItemAsync(
        Guid routeId, Guid orderItemId, CancellationToken ct) =>
        db.Set<HubSortingProgress>()
            .FirstOrDefaultAsync(p =>
                p.RouteId == routeId && p.OrderItemId == orderItemId && p.DeletedAt == null, ct);

    public async Task<IReadOnlyList<HubSortingProgress>> ListByRouteAsync(Guid routeId, CancellationToken ct) =>
        await db.Set<HubSortingProgress>()
            .AsNoTracking()
            .Where(p => p.RouteId == routeId && p.DeletedAt == null)
            .ToListAsync(ct);

    public async Task AddAsync(HubSortingProgress progress, CancellationToken ct) =>
        await db.Set<HubSortingProgress>().AddAsync(progress, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
