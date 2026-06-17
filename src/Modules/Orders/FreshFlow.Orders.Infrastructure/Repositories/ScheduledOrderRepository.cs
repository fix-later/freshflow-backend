using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.Repositories;

internal sealed class ScheduledOrderRepository(AppDbContext db) : IScheduledOrderRepository
{
    public Task<ScheduledOrder?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<ScheduledOrder>()
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);

    public async Task<IReadOnlyList<ScheduledOrder>> GetByRestaurantIdAsync(
        Guid restaurantId, CancellationToken ct) =>
        await db.Set<ScheduledOrder>()
            .Where(s => s.RestaurantId == restaurantId && s.DeletedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ScheduledOrder>> GetActiveAsync(CancellationToken ct) =>
        await db.Set<ScheduledOrder>()
            .Where(s => s.CancelledAt == null && s.DeletedAt == null)
            .ToListAsync(ct);

    public async Task AddAsync(ScheduledOrder scheduledOrder, CancellationToken ct) =>
        await db.Set<ScheduledOrder>().AddAsync(scheduledOrder, ct);

    public void Track(ScheduledOrder scheduledOrder) => db.Update(scheduledOrder);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
