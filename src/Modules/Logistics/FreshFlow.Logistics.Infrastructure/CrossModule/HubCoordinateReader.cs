using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class HubCoordinateReader(AppDbContext db) : IHubCoordinateReader
{
    public async Task<HubCoordinateDto?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<HubCoordinateRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(hub => hub.Id == id, ct);

        return row is null
            ? null
            : Map(row);
    }

    public async Task<HubCoordinateDto?> FindByMarketIdAsync(Guid marketId, CancellationToken ct)
    {
        var row = await db.Set<HubCoordinateRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(hub => hub.MarketId == marketId, ct);

        return row is null ? null : Map(row);
    }

    private static HubCoordinateDto Map(HubCoordinateRow row) =>
        new(row.Id, row.MarketId, row.Name, row.Latitude, row.Longitude);
}
