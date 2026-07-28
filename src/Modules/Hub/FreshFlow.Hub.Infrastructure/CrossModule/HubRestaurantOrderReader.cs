using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class HubRestaurantOrderReader(AppDbContext db) : IHubRestaurantOrderReader
{
    public async Task<IReadOnlyList<HubRestaurantOrder>> ListByHubAndServiceDateAsync(
        Guid hubId,
        IReadOnlyCollection<string> statuses,
        DateOnly serviceDate,
        CancellationToken ct)
    {
        var (startUtc, endUtc) = VietnamTime.GetUtcDayBounds(serviceDate);

        return await db.Set<HubRestaurantOrderRow>()
            .AsNoTracking()
            .Where(row =>
                row.HubId == hubId &&
                statuses.Contains(row.Status) &&
                row.ScheduledFor != null &&
                row.ScheduledFor >= startUtc &&
                row.ScheduledFor < endUtc)
            .Select(row => new HubRestaurantOrder(
                row.OrderId, row.RestaurantId, row.RestaurantName))
            .Distinct()
            .ToListAsync(ct);
    }
}
