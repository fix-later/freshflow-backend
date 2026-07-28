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

    public async Task<IReadOnlyList<(Guid RestaurantId, int OrderCount)>> ListRoutableRestaurantsAsync(
        DateOnly serviceDate,
        IReadOnlyCollection<string> statuses,
        CancellationToken ct)
    {
        // ScheduledFor is a UTC timestamptz; serviceDate is an Asia/Ho_Chi_Minh business date.
        // Filter by the VN-day's UTC [start, end) window so early-morning VN orders aren't misbucketed.
        var (startUtc, endUtc) = VietnamTime.GetUtcDayBounds(serviceDate);

        var rows = await db.Set<OrderStatusRow>()
            .AsNoTracking()
            .Where(o => statuses.Contains(o.Status)
                        && o.ScheduledFor != null
                        && o.ScheduledFor >= startUtc
                        && o.ScheduledFor < endUtc)
            .GroupBy(o => o.RestaurantId)
            .Select(group => new { RestaurantId = group.Key, OrderCount = group.Count() })
            .ToListAsync(ct);

        return rows.Select(row => (row.RestaurantId, row.OrderCount)).ToList();
    }
}
