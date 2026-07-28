using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class HubOrderLineReader(AppDbContext db) : IHubOrderLineReader
{
    public async Task<IReadOnlyList<HubOrderLineDto>> GetLinesByOrdersAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct)
    {
        if (orderIds.Count == 0)
            return [];

        return await db.Set<HubOrderLineRow>()
            .AsNoTracking()
            .Where(row => orderIds.Contains(row.OrderId))
            .Select(row => new HubOrderLineDto(
                row.OrderId,
                row.OrderItemId,
                row.ProductName,
                row.MarketProductId,
                row.ProductId,
                row.Unit,
                row.Quantity,
                row.CapacityKg))
            .ToListAsync(ct);
    }
}
