using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class HubDiscrepancyStatusReader(AppDbContext db) : IHubDiscrepancyStatusReader
{
    public async Task<IReadOnlyList<Guid>> GetOrdersWithOpenDiscrepanciesAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct)
    {
        if (orderIds.Count == 0)
            return [];

        return await db.Set<HubDiscrepancyStatusRow>()
            .AsNoTracking()
            .Where(row => orderIds.Contains(row.OrderId))
            .Select(row => row.OrderId)
            .ToListAsync(ct);
    }
}
