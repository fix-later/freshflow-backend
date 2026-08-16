using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class ProcurementMetricsReader(AppDbContext db) : IProcurementMetricsReader
{
    public async Task<ProcurementMetricsReadModel> ReadAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        Guid? marketId,
        CancellationToken ct)
    {
        var batches = db.Set<ProcurementBatchRow>()
            .AsNoTracking()
            .Where(row => row.BatchDate >= fromInclusive && row.BatchDate <= toInclusive);

        if (marketId is { } id)
        {
            batches = batches.Where(row => row.MarketId == id);
        }

        var statusCounts = await batches
            .GroupBy(row => row.Status)
            .Select(group => new ProcurementNamedCountReadModel(group.Key, group.Count()))
            .ToListAsync(ct);

        var leadTimeMinutes = await batches
            .Where(row => row.ManifestedAt != null && row.HandedOffAt != null)
            .AverageAsync(
                row => (double?)(row.HandedOffAt!.Value - row.ManifestedAt!.Value).TotalMinutes,
                ct);

        var items =
            from item in db.Set<ProcurementBatchItemRow>().AsNoTracking()
            join batch in batches on item.ProcurementBatchId equals batch.BatchId
            select item;
        var itemSummary = await items
            .GroupBy(_ => 1)
            .Select(group => new ItemSummary(
                group.Count(),
                group.Count(row => row.ActualQuantity != null),
                group.Sum(row =>
                    row.ActualQuantity != null && row.ActualUnitPrice != null
                        ? (decimal?)(row.ActualQuantity.Value * row.ActualUnitPrice.Value)
                        : null)))
            .SingleOrDefaultAsync(ct);
        var priceVariancePercent = await items
            .Where(row =>
                row.ActualUnitPrice != null &&
                row.ReferenceUnitPrice != null &&
                row.ReferenceUnitPrice > 0m)
            .AverageAsync(
                row => (decimal?)((row.ActualUnitPrice!.Value - row.ReferenceUnitPrice!.Value) /
                    row.ReferenceUnitPrice.Value * 100m),
                ct);

        var exceptionsByType = await (
                from exception in db.Set<ProcurementExceptionRow>().AsNoTracking()
                join batch in batches on exception.ProcurementBatchId equals batch.BatchId
                group exception by exception.Type
                into exceptions
                select new ProcurementNamedCountReadModel(exceptions.Key, exceptions.Count()))
            .ToListAsync(ct);

        return new ProcurementMetricsReadModel(
            statusCounts,
            itemSummary?.ItemsTotal ?? 0,
            itemSummary?.ItemsPurchased ?? 0,
            itemSummary?.TotalActualCostVND ?? 0m,
            priceVariancePercent,
            leadTimeMinutes is null ? null : (decimal)leadTimeMinutes.Value,
            exceptionsByType);
    }

    private sealed record ItemSummary(
        int ItemsTotal,
        int ItemsPurchased,
        decimal? TotalActualCostVND);
}
