using System.Data;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketSessionTrackingReader(AppDbContext db) : IMarketSessionTrackingReader
{
    public async Task<MarketSessionTrackingData> ReadAsync(
        Guid marketSessionId, int page, int pageSize, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            return await ReadCoreAsync(marketSessionId, page, pageSize, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, ct);
        var result = await ReadCoreAsync(marketSessionId, page, pageSize, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private async Task<MarketSessionTrackingData> ReadCoreAsync(
        Guid marketSessionId, int page, int pageSize, CancellationToken ct)
    {
        var rows = db.Set<MarketSessionTrackingOrderRow>()
            .AsNoTracking()
            .Where(row => row.MarketSessionId == marketSessionId);
        var totalOrders = await rows.Select(row => row.OrderId).Distinct().CountAsync(ct);
        var cancelledOrders = await rows
            .Where(row => row.Status == "Cancelled")
            .Select(row => row.OrderId)
            .Distinct()
            .CountAsync(ct);
        var activeLines = rows.Where(row =>
            row.Status != "Cancelled" &&
            row.OrderItemId != null);

        var totalLineItems = await activeLines.CountAsync(ct);
        var totalQuantity = await activeLines.SumAsync(
            row => (long?)(row.Quantity ?? 0), ct) ?? 0;
        var activeOrderTotals = rows
            .Where(row => row.Status != "Cancelled")
            .GroupBy(row => row.OrderId)
            .Select(group => new
            {
                SubtotalAmount = group.Max(row => row.SubtotalAmount),
                VatAmount = group.Max(row => row.VatAmount),
                DeliveryFee = group.Max(row => row.DeliveryFee),
                TotalAmount = group.Max(row => row.TotalAmount)
            });
        var merchandiseAmount = await activeOrderTotals.SumAsync(row => row.SubtotalAmount, ct);
        var vatAmount = await activeOrderTotals.SumAsync(row => row.VatAmount, ct);
        var deliveryFee = await activeOrderTotals.SumAsync(row => row.DeliveryFee, ct);
        var grandTotal = await activeOrderTotals.SumAsync(row => row.TotalAmount, ct);
        var productRows = await activeLines
            .GroupBy(row => new { row.MarketProductId, row.ProductName })
            .Select(group => new
            {
                group.Key.MarketProductId,
                group.Key.ProductName,
                OrderCount = group.Select(row => row.OrderId).Distinct().Count(),
                TotalQuantity = group.Sum(row => (long)(row.Quantity ?? 0))
            })
            .OrderBy(product => product.ProductName)
            .ToListAsync(ct);
        var products = productRows.Select(product => new MarketSessionTrackingProductDto(
            product.MarketProductId!.Value,
            product.ProductName!,
            product.OrderCount,
            product.TotalQuantity)).ToList();

        var pageOrderIds = await rows
            .GroupBy(row => row.OrderId)
            .Select(group => new { OrderId = group.Key, ConfirmedAt = group.Max(row => row.ConfirmedAt) })
            .OrderByDescending(order => order.ConfirmedAt)
            .ThenByDescending(order => order.OrderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => order.OrderId)
            .ToListAsync(ct);
        var pageRows = pageOrderIds.Count == 0
            ? []
            : await rows
                .Where(row => pageOrderIds.Contains(row.OrderId))
                .OrderBy(row => row.ProductName)
                .ToListAsync(ct);
        var pageHeaders = pageRows
            .GroupBy(row => row.OrderId)
            .Select(group => group.First())
            .OrderByDescending(order => order.ConfirmedAt)
            .ThenByDescending(order => order.OrderId)
            .ToList();
        var itemRows = pageRows.Where(row => row.OrderItemId != null);
        var itemsByOrder = itemRows.ToLookup(row => row.OrderId);
        var orders = pageHeaders.Select(order => new MarketSessionTrackingOrderDto(
                order.OrderId,
                order.RestaurantId,
                order.RestaurantName,
                order.Status.ToLowerInvariant(),
                order.SubtotalAmount,
                order.VatAmount,
                order.DeliveryFee,
                order.TotalAmount,
                order.ConfirmedAt,
                itemsByOrder[order.OrderId]
                    .Select(item => new MarketSessionTrackingOrderItemDto(
                        item.MarketProductId!.Value,
                        item.ProductName!,
                        item.Quantity!.Value,
                        item.UnitPrice!.Value,
                        item.Subtotal!.Value))
                    .ToList()
                    .AsReadOnly()))
            .ToList()
            .AsReadOnly();

        var activeBatchEntity = await db.Set<ProcurementBatch>()
            .AsNoTracking()
            .Where(batch =>
                batch.MarketSessionId == marketSessionId &&
                batch.Status != ProcurementBatchStatus.Cancelled &&
                batch.DeletedAt == null)
            .OrderByDescending(batch => batch.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var activeBatch = activeBatchEntity is null
            ? null
            : new MarketSessionTrackingBatchDto(
                activeBatchEntity.Id, activeBatchEntity.Code, activeBatchEntity.Status.ToString());

        return new MarketSessionTrackingData(
            new MarketSessionTrackingSummaryDto(
                totalOrders,
                totalOrders - cancelledOrders,
                cancelledOrders,
                totalLineItems,
                totalQuantity,
                merchandiseAmount,
                vatAmount,
                deliveryFee,
                grandTotal),
            products.AsReadOnly(),
            orders,
            new ProcurementBatchPaginationDto(totalOrders, page, pageSize),
            activeBatch);
    }

}
