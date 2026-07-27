using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class HubProcurementPlanReader(AppDbContext db) : IHubProcurementPlanReader
{
    public Task<bool> HasOpenBatchesAsync(Guid hubId, CancellationToken ct) =>
        db.Set<HubProcurementBatchRow>()
            .AsNoTracking()
            .AnyAsync(
                batch =>
                    batch.HubId == hubId &&
                    batch.Status != "HandedOff" &&
                    batch.Status != "Cancelled",
                ct);

    public async Task<HubProcurementPlanDto> ReadAsync(
        Guid hubId,
        DateOnly date,
        CancellationToken ct)
    {
        var batches = await db.Set<HubProcurementBatchRow>()
            .AsNoTracking()
            .Where(batch => batch.HubId == hubId && batch.BatchDate == date)
            .OrderBy(batch => batch.BatchId)
            .ToListAsync(ct);

        if (batches.Count == 0)
            return new HubProcurementPlanDto(hubId, date, []);

        var batchIds = batches.Select(batch => batch.BatchId).ToArray();
        var items = await db.Set<HubProcurementItemRow>()
            .AsNoTracking()
            .Where(item => batchIds.Contains(item.ProcurementBatchId))
            .OrderBy(item => item.MarketProductId)
            .ToListAsync(ct);
        var orders = await db.Set<HubProcurementOrderRow>()
            .AsNoTracking()
            .Where(order => batchIds.Contains(order.ProcurementBatchId))
            .OrderBy(order => order.OrderId)
            .ToListAsync(ct);

        var itemsByBatch = items.ToLookup(item => item.ProcurementBatchId);
        var ordersByBatch = orders.ToLookup(order => order.ProcurementBatchId);
        var result = batches.Select(batch => new HubProcurementBatchDto(
                batch.BatchId,
                batch.MarketId,
                batch.Status,
                batch.HandedOffAt,
                ordersByBatch[batch.BatchId].Select(order => order.OrderId).ToList(),
                itemsByBatch[batch.BatchId]
                    .Select(item => new HubProcurementItemDto(
                        item.MarketProductId,
                        item.ProductName,
                        item.TargetQuantity,
                        item.ActualQuantity,
                        item.ActualUnitPrice,
                        item.PurchasedAt))
                    .ToList()))
            .ToList();

        return new HubProcurementPlanDto(hubId, date, result);
    }

    public async Task<IReadOnlyList<HubProcurementItemDto>> ReadBatchItemsAsync(
        Guid batchId,
        CancellationToken ct)
    {
        var items = await db.Set<HubProcurementItemRow>()
            .AsNoTracking()
            .Where(item => item.ProcurementBatchId == batchId)
            .OrderBy(item => item.MarketProductId)
            .ToListAsync(ct);

        return items
            .Select(item => new HubProcurementItemDto(
                item.MarketProductId,
                item.ProductName,
                item.TargetQuantity,
                item.ActualQuantity,
                item.ActualUnitPrice,
                item.PurchasedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<HubHandedOffBatchDto>> ReadHandedOffBatchesAsync(CancellationToken ct)
    {
        var batches = await db.Set<HubProcurementBatchRow>()
            .AsNoTracking()
            .Where(batch => batch.Status == "HandedOff" && batch.HubId != null)
            .OrderBy(batch => batch.BatchId)
            .ToListAsync(ct);

        return batches
            .Select(batch => new HubHandedOffBatchDto(
                batch.BatchId,
                batch.HubId!.Value,
                batch.MarketId,
                batch.HandedOffAt ?? DateTime.UtcNow,
                batch.AssignedAgentUserId))
            .ToList();
    }
}
