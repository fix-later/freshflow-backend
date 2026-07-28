using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class HubSortingStateReader(AppDbContext db) : IHubSortingStateReader
{
    public Task<bool> HasSortedLinesAsync(
        Guid routeId,
        Guid marketId,
        DateOnly serviceDate,
        CancellationToken ct) =>
        db.Set<HubSortingStateRow>()
            .AsNoTracking()
            .AnyAsync(
                row =>
                    row.Status == "SORTED" &&
                    (row.RouteId == routeId ||
                     (row.RouteId == null &&
                      row.MarketId == marketId &&
                      row.ServiceDate == serviceDate)),
                ct);
}
