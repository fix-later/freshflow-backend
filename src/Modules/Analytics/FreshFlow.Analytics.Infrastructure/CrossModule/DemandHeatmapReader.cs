using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class DemandHeatmapReader(AppDbContext db) : IDemandHeatmapReader
{
    private const double VietnamUtcOffsetHours = 7d;

    public async Task<IReadOnlyList<DemandHeatmapAggregateReadModel>> ReadHeatmapAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct)
    {
        var orders = ScopedOrders(startUtcInclusive, endUtcExclusive);
        var aggregates = await (
            from order in orders
            join coordinate in db.Set<RestaurantCoordinateRow>().AsNoTracking()
                on order.RestaurantId equals coordinate.RestaurantId
            where coordinate.Latitude.HasValue && coordinate.Longitude.HasValue
            group order by new
            {
                order.RestaurantId,
                RestaurantName = coordinate.Name,
                Latitude = coordinate.Latitude!.Value,
                Longitude = coordinate.Longitude!.Value,
                order.Status
            }
            into groupRows
            orderby groupRows.Key.RestaurantName, groupRows.Key.RestaurantId, groupRows.Key.Status
            select new RestaurantDemandAggregate(
                groupRows.Key.RestaurantId,
                groupRows.Key.RestaurantName,
                groupRows.Key.Latitude,
                groupRows.Key.Longitude,
                groupRows.Key.Status,
                groupRows.Count(),
                groupRows.Sum(row => row.TotalAmount)))
            .ToListAsync(ct);

        var categoryTotals = await (
            from order in orders
            join item in db.Set<OrderItemCategoryRow>().AsNoTracking()
                on order.OrderId equals item.OrderId
            where item.CategoryName != null
            group item by new { order.RestaurantId, item.CategoryName }
            into groupRows
            select new CategoryAggregate(
                groupRows.Key.RestaurantId,
                groupRows.Key.CategoryName!,
                groupRows.Sum(item => item.Quantity)))
            .ToListAsync(ct);
        var dominantCategories = categoryTotals
            .GroupBy(row => row.RestaurantId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(row => row.Quantity)
                    .ThenBy(row => row.CategoryName)
                    .First()
                    .CategoryName);

        return aggregates
            .Select(row => new DemandHeatmapAggregateReadModel(
                row.RestaurantId,
                row.RestaurantName,
                row.Latitude,
                row.Longitude,
                row.Status,
                row.OrderCount,
                row.TotalOrderValueVND,
                dominantCategories.GetValueOrDefault(row.RestaurantId)))
            .ToArray();
    }

    public async Task<IReadOnlyList<TimeDistributionCellReadModel>> ReadTimeDistributionAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct)
    {
        // DateTime.DayOfWeek and PostgreSQL date_part('dow') both use Sunday = 0.
        var rows = await ScopedOrders(startUtcInclusive, endUtcExclusive)
            .GroupBy(row => new
            {
                DayOfWeek = row.CreatedAt.AddHours(VietnamUtcOffsetHours).DayOfWeek,
                HourOfDay = row.CreatedAt.AddHours(VietnamUtcOffsetHours).Hour
            })
            .Select(group => new
            {
                group.Key.DayOfWeek,
                group.Key.HourOfDay,
                OrderCount = group.Count()
            })
            .ToListAsync(ct);

        return rows
            .OrderBy(row => row.DayOfWeek)
            .ThenBy(row => row.HourOfDay)
            .Select(row => new TimeDistributionCellReadModel(
                (int)row.DayOfWeek,
                row.HourOfDay,
                row.OrderCount))
            .ToArray();
    }

    private IQueryable<OrderSummaryRow> ScopedOrders(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive) =>
        db.Set<OrderSummaryRow>()
            .AsNoTracking()
            .Where(row => row.CreatedAt >= startUtcInclusive && row.CreatedAt < endUtcExclusive);

    private sealed record RestaurantDemandAggregate(
        Guid RestaurantId,
        string RestaurantName,
        decimal Latitude,
        decimal Longitude,
        string Status,
        int OrderCount,
        decimal TotalOrderValueVND);

    private sealed record CategoryAggregate(
        Guid RestaurantId,
        string CategoryName,
        int Quantity);
}
