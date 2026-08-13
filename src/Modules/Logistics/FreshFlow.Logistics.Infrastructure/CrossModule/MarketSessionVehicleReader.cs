using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class MarketSessionVehicleReader(AppDbContext db) : IMarketSessionVehicleReader
{
    public async Task<IReadOnlySet<Guid>> ReadAssignedVehicleIdsAsync(
        Guid hubId, DateOnly serviceDate, CancellationToken ct) =>
        (await db.Set<MarketSessionVehicleAssignmentRow>()
            .AsNoTracking()
            .Where(row => row.HubId == hubId && row.ServiceDate == serviceDate)
            .Select(row => row.VehicleId)
            .ToListAsync(ct))
        .ToHashSet();
}
