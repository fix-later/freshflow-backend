using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.Repositories;

internal sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    public Task<Order?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<Order>()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Order>> GetByRestaurantIdAsync(Guid restaurantId, CancellationToken ct) =>
        await db.Set<Order>()
            .Include(o => o.Items)
            .Where(o => o.RestaurantId == restaurantId && o.DeletedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Order> Orders, int Total)> SearchAsync(
        OrderSearchCriteria criteria, CancellationToken ct)
    {
        var query = db.Set<Order>()
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.DeletedAt == null);

        if (criteria.RestaurantId.HasValue)
            query = query.Where(o => o.RestaurantId == criteria.RestaurantId.Value);

        if (criteria.Status.HasValue)
            query = query.Where(o => o.Status == criteria.Status.Value);

        if (criteria.CreatedFrom.HasValue)
            query = query.Where(o => o.CreatedAt >= criteria.CreatedFrom.Value);

        if (criteria.CreatedTo.HasValue)
            query = query.Where(o => o.CreatedAt <= criteria.CreatedTo.Value);

        var total = await query.CountAsync(ct);

        query = criteria.SortAscending
            ? query.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id)
            : query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id);

        var orders = await query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return (orders.AsReadOnly(), total);
    }

    public async Task<(IReadOnlyList<Order> Orders, int Total)> GetByScheduledOrderIdAsync(
        Guid scheduledOrderId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Set<Order>()
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.ScheduledOrderId == scheduledOrderId && o.DeletedAt == null);

        var total = await query.CountAsync(ct);
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (orders.AsReadOnly(), total);
    }

    public async Task AddAsync(Order order, CancellationToken ct) =>
        await db.Set<Order>().AddAsync(order, ct);

    public void Track(Order order)
    {
        var entry = db.Entry(order);
        if (entry.State == EntityState.Detached)
            db.Attach(order);

        entry.State = EntityState.Modified;
    }

    public void TrackNewItem(OrderItem item) =>
        db.Entry(item).State = EntityState.Added;

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
