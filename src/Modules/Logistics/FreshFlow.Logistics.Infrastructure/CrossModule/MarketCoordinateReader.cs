using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class MarketCoordinateReader(AppDbContext db) : IMarketCoordinateReader
{
    public async Task<MarketCoordinateDto?> FindByIdAsync(Guid marketId, CancellationToken ct)
    {
        var row = await db.Set<MarketCoordinateRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == marketId, ct);

        return row is null
            ? null
            : new MarketCoordinateDto(row.Id, row.Name, row.Latitude, row.Longitude);
    }
}
