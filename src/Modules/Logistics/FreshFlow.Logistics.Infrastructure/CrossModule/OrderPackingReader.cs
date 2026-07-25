using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class OrderPackingReader(AppDbContext db) : IOrderPackingReader
{
    public async Task<IReadOnlyList<OrderPackingLine>> GetLinesAsync(
        Guid orderId, CancellationToken ct) =>
        await db.Set<OrderPackingLineRow>()
            .AsNoTracking()
            .Where(row => row.OrderId == orderId)
            .Select(row => new OrderPackingLine(
                row.ProductName, row.Quantity, row.CapacityKg))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrderPackingLines>> GetLinesByOrdersAsync(
        IReadOnlyCollection<Guid> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0)
            return [];

        var rows = await db.Set<OrderPackingLineRow>()
            .AsNoTracking()
            .Where(row => orderIds.Contains(row.OrderId))
            .ToListAsync(ct);

        return rows
            .GroupBy(row => row.OrderId)
            .Select(group => new OrderPackingLines(
                group.Key,
                group.Select(row => new OrderPackingLine(row.ProductName, row.Quantity, row.CapacityKg)).ToList()))
            .ToList();
    }
}
