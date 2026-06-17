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
