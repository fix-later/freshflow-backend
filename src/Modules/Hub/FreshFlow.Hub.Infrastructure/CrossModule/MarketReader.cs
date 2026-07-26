using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class MarketReader(AppDbContext db) : IMarketReader
{
    public async Task<MarketSnapshot?> FindAsync(Guid marketId, CancellationToken ct)
    {
        var row = await db.Set<MarketRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(market => market.Id == marketId, ct);

        return row is null ? null : new MarketSnapshot(row.Id, row.IsActive);
    }
}
