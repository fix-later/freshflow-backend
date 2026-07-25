using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class OrderStatusReader(AppDbContext db) : IOrderStatusReader
{
    public async Task<OrderStatusLookupDto?> FindByIdAsync(Guid orderId, CancellationToken ct)
    {
        var row = await db.Set<OrderStatusRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

        return row is null ? null : new OrderStatusLookupDto(row.OrderId, row.Status, row.RestaurantId);
    }

    public async Task<IReadOnlyList<OrderStatusLookupDto>> ListByRestaurantsAndStatusAsync(
        IReadOnlyCollection<Guid> restaurantIds, string status, CancellationToken ct)
    {
        if (restaurantIds.Count == 0)
            return [];

        return await db.Set<OrderStatusRow>()
            .AsNoTracking()
            .Where(o => o.Status == status && restaurantIds.Contains(o.RestaurantId))
            .Select(o => new OrderStatusLookupDto(o.OrderId, o.Status, o.RestaurantId))
            .ToListAsync(ct);
    }
}
