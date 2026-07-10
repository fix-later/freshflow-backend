using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class OrderLookupReader(AppDbContext db) : IOrderLookupReader
{
    public async Task<OrderLookupDto?> FindByOrderItemIdAsync(Guid orderItemId, CancellationToken ct)
    {
        var row = await db.Set<OrderLookupRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderItemId == orderItemId, ct);

        return row is null ? null : new OrderLookupDto(row.OrderItemId, row.OrderId);
    }
}
