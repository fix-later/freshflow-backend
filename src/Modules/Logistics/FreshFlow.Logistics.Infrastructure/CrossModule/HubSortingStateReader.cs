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
                      row.HubId == hubId &&
                      row.ServiceDate == serviceDate)),
                ct);
}
