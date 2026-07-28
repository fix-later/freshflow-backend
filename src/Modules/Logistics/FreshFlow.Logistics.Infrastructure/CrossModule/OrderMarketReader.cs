using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class OrderMarketReader(AppDbContext db) : IOrderMarketReader
{
    public async Task<IReadOnlyList<(Guid MarketId, string MarketName, int OrderCount)>>
        ListRoutableMarketsAsync(
            DateOnly serviceDate,
            IReadOnlyCollection<string> statuses,
            CancellationToken ct)
    {
        // ScheduledFor is a UTC timestamptz; serviceDate is an Asia/Ho_Chi_Minh business date.
        // Filter by the VN-day's UTC [start, end) window so early-morning VN orders aren't misbucketed.
        var (startUtc, endUtc) = VietnamTime.GetUtcDayBounds(serviceDate);

        var rows = await db.Set<OrderMarketRow>()
            .AsNoTracking()
            .Where(row => statuses.Contains(row.Status)
                          && row.ScheduledFor != null
                          && row.ScheduledFor >= startUtc
                          && row.ScheduledFor < endUtc)
            .GroupBy(row => new { row.MarketId, row.MarketName })
            .Select(group => new
            {
                group.Key.MarketId,
                group.Key.MarketName,
                OrderCount = group.Select(row => row.OrderId).Distinct().Count()
            })
            .ToListAsync(ct);

        return rows.Select(row => (row.MarketId, row.MarketName, row.OrderCount)).ToList();
    }
}
