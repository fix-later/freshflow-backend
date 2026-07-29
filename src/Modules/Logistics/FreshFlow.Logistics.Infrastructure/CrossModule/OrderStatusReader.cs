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

        return row is null
            ? null
            : new OrderStatusLookupDto(row.OrderId, row.Status, row.RestaurantId, row.HubId);
    }

    public async Task<IReadOnlyList<OrderStatusLookupDto>> ListByRestaurantsAndStatusAsync(
        IReadOnlyCollection<Guid> restaurantIds,
        string status,
        CancellationToken ct,
        Guid? hubId = null,
        DateOnly? serviceDate = null)
    {
        if (restaurantIds.Count == 0)
            return [];

        var query = db.Set<OrderStatusRow>()
            .AsNoTracking()
            .Where(o => o.Status == status
                        && restaurantIds.Contains(o.RestaurantId)
                        && (hubId == null || o.HubId == hubId));

        if (serviceDate is { } date)
        {
            var (startUtc, endUtc) = VietnamTime.GetUtcDayBounds(date);
            query = query.Where(o => o.ScheduledFor >= startUtc && o.ScheduledFor < endUtc);
        }

        return await query
            .Select(o => new OrderStatusLookupDto(o.OrderId, o.Status, o.RestaurantId, o.HubId))
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
