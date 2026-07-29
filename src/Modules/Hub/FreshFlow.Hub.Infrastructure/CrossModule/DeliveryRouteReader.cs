using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class DeliveryRouteReader(AppDbContext db) : IDeliveryRouteReader
{
    public async Task<DeliveryRouteLookupDto?> FindByIdAsync(Guid routeId, CancellationToken ct)
    {
        var row = await db.Set<DeliveryRouteRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RouteId == routeId, ct);

        return row is null
            ? null
            : new DeliveryRouteLookupDto(row.RouteId, row.HubId, row.Status, row.DriverUserId);
    }
}
