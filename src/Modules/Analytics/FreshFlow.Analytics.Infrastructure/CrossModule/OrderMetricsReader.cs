using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class OrderMetricsReader(AppDbContext db) : IOrderMetricsReader
{
    private const double VietnamUtcOffsetHours = 7d;

    public async Task<IReadOnlyList<OrderMetricsBucketReadModel>> ReadAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        Guid? restaurantId,
        string groupBy,
        CancellationToken ct)
    {
        var orders = db.Set<OrderSummaryRow>()
            .AsNoTracking()
            .Where(row => row.CreatedAt >= startUtcInclusive && row.CreatedAt < endUtcExclusive);

        if (restaurantId is { } id)
        {
            orders = orders.Where(row => row.RestaurantId == id);
        }

        var aggregatesQuery = groupBy switch
        {
            "week" => orders
                .GroupBy(row => new
                {
                    Date = row.CreatedAt.AddHours(VietnamUtcOffsetHours).Date.AddDays(
                        -(((int)row.CreatedAt.AddHours(VietnamUtcOffsetHours).DayOfWeek + 6) % 7)),
                    row.Status
                })
                .Select(group => new BucketAggregate(
                    group.Key.Date,
                    group.Key.Status,
                    group.Count(),
                    group.Sum(row => row.TotalAmount))),
            "month" => orders
                .GroupBy(row => new
                {
                    Date = row.CreatedAt.AddHours(VietnamUtcOffsetHours).Date.AddDays(
                        1 - row.CreatedAt.AddHours(VietnamUtcOffsetHours).Day),
                    row.Status
                })
                .Select(group => new BucketAggregate(
                    group.Key.Date,
                    group.Key.Status,
                    group.Count(),
                    group.Sum(row => row.TotalAmount))),
            _ => orders
                .GroupBy(row => new
                {
                    Date = row.CreatedAt.AddHours(VietnamUtcOffsetHours).Date,
                    row.Status
                })
                .Select(group => new BucketAggregate(
                    group.Key.Date,
                    group.Key.Status,
                    group.Count(),
                    group.Sum(row => row.TotalAmount)))
        };

        var aggregates = await aggregatesQuery.ToListAsync(ct);

        return aggregates
            .Select(row => new OrderMetricsBucketReadModel(
                DateOnly.FromDateTime(row.Date),
                row.Status,
                row.OrderCount,
                row.TotalAmountVND))
            .ToArray();
    }

    private sealed record BucketAggregate(
        DateTime Date,
        string Status,
        int OrderCount,
        decimal TotalAmountVND);
}
