using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class HubSortingStateReader(AppDbContext db) : IHubSortingStateReader
{
    public Task<bool> HasSortedLinesAsync(
        Guid routeId,
        Guid hubId,
        DateOnly serviceDate,
        CancellationToken ct) =>
        db.Set<HubSortingStateRow>()
            .AsNoTracking()
            .AnyAsync(
                row =>
                    row.Status == "SORTED" &&
                    (row.RouteId == routeId ||
                     (row.RouteId == null &&
                      // The caller passes route.HubId when set, else the route's market-stop id.
                      // Match either the hub or its market so a market-level sort still locks.
                      (row.HubId == hubId || row.MarketId == hubId) &&
                      row.ServiceDate == serviceDate)),
                ct);
}
