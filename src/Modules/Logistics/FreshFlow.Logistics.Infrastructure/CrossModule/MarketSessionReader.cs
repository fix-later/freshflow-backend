using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class MarketSessionReader(AppDbContext db) : IMarketSessionReader
{
    public async Task<MarketSessionLookupDto?> FindByIdAsync(Guid sessionId, CancellationToken ct)
    {
        var row = await db.Set<MarketSessionRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == sessionId, ct);

        return row is null
            ? null
            : new MarketSessionLookupDto(row.Id, row.HubId, row.ServiceDate, row.Status);
    }
}
