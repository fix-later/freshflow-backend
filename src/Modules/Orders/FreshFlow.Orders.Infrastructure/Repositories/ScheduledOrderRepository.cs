using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.Repositories;

internal sealed class ScheduledOrderRepository(AppDbContext db) : IScheduledOrderRepository
{
    public Task<ScheduledOrder?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<ScheduledOrder>()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);

    public async Task<IReadOnlyList<ScheduledOrder>> GetByRestaurantIdAsync(
        Guid restaurantId, CancellationToken ct) =>
        await db.Set<ScheduledOrder>()
            .Include(s => s.Items)
            .Where(s => s.RestaurantId == restaurantId && s.DeletedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ScheduledOrder>> GetActiveAsync(CancellationToken ct) =>
        await db.Set<ScheduledOrder>()
            .Include(s => s.Items)
            .Where(s => s.CancelledAt == null && s.DeletedAt == null)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<ScheduledOrder> ScheduledOrders, int Total)> SearchAsync(
        ScheduledOrderSearchCriteria criteria, CancellationToken ct)
    {
        var query = db.Set<ScheduledOrder>()
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.DeletedAt == null);

        if (criteria.RestaurantId.HasValue)
            query = query.Where(s => s.RestaurantId == criteria.RestaurantId.Value);

        if (!criteria.IncludeCancelled)
            query = query.Where(s => s.CancelledAt == null);

        var total = await query.CountAsync(ct);
        var scheduledOrders = await query
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return (scheduledOrders.AsReadOnly(), total);
    }

    public async Task AddAsync(ScheduledOrder scheduledOrder, CancellationToken ct) =>
        await db.Set<ScheduledOrder>().AddAsync(scheduledOrder, ct);

    public void Track(ScheduledOrder scheduledOrder) => db.Update(scheduledOrder);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
