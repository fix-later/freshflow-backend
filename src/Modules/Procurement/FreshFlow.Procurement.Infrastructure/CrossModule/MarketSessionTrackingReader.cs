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
        var headers = rows
            .Select(row => new TrackingOrderHeader(
                row.OrderId,
                row.RestaurantId,
                row.RestaurantName,
                row.Status,
                row.TotalAmount,
                row.ConfirmedAt))
            .Distinct();

        var totalOrders = await headers.CountAsync(ct);
        var cancelledOrders = await headers.CountAsync(
            order => order.Status == "Cancelled", ct);
        var activeLines = rows.Where(row =>
            row.Status != "Cancelled" &&
            row.OrderItemId != null);

        var totalLineItems = await activeLines.CountAsync(ct);
        var totalQuantity = await activeLines.SumAsync(
            row => (long?)(row.Quantity ?? 0), ct) ?? 0;
        var products = await activeLines
            .GroupBy(row => row.MarketProductId)
            .Select(group => new MarketSessionTrackingProductDto(
                group.Key!.Value,
                group.Max(row => row.ProductName)!,
                group.Select(row => row.OrderId).Distinct().Count(),
                group.Sum(row => (long)(row.Quantity ?? 0))))
            .OrderBy(product => product.ProductName)
            .ToListAsync(ct);

        var pageHeaders = await headers
            .OrderByDescending(order => order.ConfirmedAt)
            .ThenByDescending(order => order.OrderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var pageOrderIds = pageHeaders.Select(order => order.OrderId).ToArray();
        var itemRows = pageOrderIds.Length == 0
            ? []
            : await rows
                .Where(row => pageOrderIds.Contains(row.OrderId) && row.OrderItemId != null)
                .OrderBy(row => row.ProductName)
                .ToListAsync(ct);
        var itemsByOrder = itemRows.ToLookup(row => row.OrderId);
        var orders = pageHeaders.Select(order => new MarketSessionTrackingOrderDto(
                order.OrderId,
                order.RestaurantId,
                order.RestaurantName,
                order.Status.ToLowerInvariant(),
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
                totalQuantity),
            products.AsReadOnly(),
            orders,
            new ProcurementBatchPaginationDto(totalOrders, page, pageSize),
            activeBatch);
    }

    private sealed record TrackingOrderHeader(
        Guid OrderId,
        Guid RestaurantId,
        string RestaurantName,
        string Status,
        decimal TotalAmount,
        DateTime? ConfirmedAt);
}
