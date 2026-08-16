using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class HubThroughputReader(AppDbContext db) : IHubThroughputReader
{
    private const string ArrivedStatus = "ARRIVED_AT_HUB";
    private const double VietnamUtcOffsetHours = 7d;

    public async Task<HubThroughputReadModel> ReadAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        Guid? hubId,
        CancellationToken ct)
    {
        var inboundEvents = db.Set<HubInboundEventRow>()
            .AsNoTracking()
            .Where(row => row.ArrivedAt >= startUtcInclusive && row.ArrivedAt < endUtcExclusive);
        var outboundEvents = db.Set<HubOutboundEventRow>()
            .AsNoTracking()
            .Where(row => row.DispatchedAt >= startUtcInclusive && row.DispatchedAt < endUtcExclusive);

        if (hubId is { } id)
        {
            inboundEvents = inboundEvents.Where(row => row.HubId == id);
            outboundEvents = outboundEvents.Where(row => row.HubId == id);
        }

        var hubs = db.Set<HubRow>().AsNoTracking();
        var inbound =
            from inboundEvent in inboundEvents
            join hub in hubs on inboundEvent.HubId equals hub.HubId
            select new { Event = inboundEvent, Hub = hub };
        var outbound =
            from outboundEvent in outboundEvents
            join hub in hubs on outboundEvent.HubId equals hub.HubId
            select new { Event = outboundEvent, Hub = hub };

        var inboundAggregates = await inbound
            .GroupBy(row => new
            {
                Date = row.Event.ArrivedAt.AddHours(VietnamUtcOffsetHours).Date,
                row.Event.HubId,
                row.Hub.HubName
            })
            .Select(group => new BucketAggregate(
                group.Key.Date,
                group.Key.HubId,
                group.Key.HubName,
                group.Sum(row => row.Event.Status == ArrivedStatus
                    ? row.Event.TotalQuantityKg
                    : 0m),
                group.Count()))
            .ToListAsync(ct);
        var outboundAggregates = await outbound
            .GroupBy(row => new
            {
                Date = row.Event.DispatchedAt.AddHours(VietnamUtcOffsetHours).Date,
                row.Event.HubId,
                row.Hub.HubName
            })
            .Select(group => new BucketAggregate(
                group.Key.Date,
                group.Key.HubId,
                group.Key.HubName,
                group.Sum(row => row.Event.TotalQuantityKg),
                group.Count()))
            .ToListAsync(ct);
        var statusCounts = await inbound
            .GroupBy(row => row.Event.Status)
            .Select(group => new HubThroughputStatusCountReadModel(group.Key, group.Count()))
            .ToListAsync(ct);

        return new HubThroughputReadModel(
            Map(inboundAggregates),
            Map(outboundAggregates),
            statusCounts);
    }

    private static IReadOnlyList<HubThroughputBucketReadModel> Map(
        IReadOnlyList<BucketAggregate> rows) =>
        rows.Select(row => new HubThroughputBucketReadModel(
            DateOnly.FromDateTime(row.Date),
            row.HubId,
            row.HubName,
            row.Kg,
            row.EventCount))
        .ToArray();

    private sealed record BucketAggregate(
        DateTime Date,
        Guid HubId,
        string HubName,
        decimal Kg,
        int EventCount);
}
