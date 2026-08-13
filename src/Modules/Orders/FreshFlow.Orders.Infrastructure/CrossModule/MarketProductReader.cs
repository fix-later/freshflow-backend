using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class MarketProductReader(AppDbContext db) : IMarketProductReader
{
    public async Task<MarketProductSnapshotDto?> FindAsync(Guid marketProductId, CancellationToken ct)
    {
        var row = await db.Set<MarketProductRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == marketProductId, ct);

        return row is null
            ? null
            : new MarketProductSnapshotDto(
                row.Id,
                row.ProductName,
                row.CurrentPrice,
                row.CurrentQuantity - row.ReservedQuantity,
                row.MinimumOrderQuantity,
                row.VatRate,
                row.OriginLatitude,
                row.OriginLongitude,
                row.MarketId);
    }
}
