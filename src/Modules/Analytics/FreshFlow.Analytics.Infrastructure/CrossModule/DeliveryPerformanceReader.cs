using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class DeliveryPerformanceReader(AppDbContext db) : IDeliveryPerformanceReader
{
    private const int LateThresholdMinutes = 15;
    private const string DeliveredStatus = "delivered";
    private const string FailedStatus = "failed";

    public async Task<DeliveryPerformanceReadModel> ReadAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct)
    {
        var deliveries = db.Set<DeliveryRow>()
            .AsNoTracking()
            .Where(row =>
                row.ActualArrival >= startUtcInclusive &&
                row.ActualArrival < endUtcExclusive);
        var delivered = deliveries.Where(row => row.Status == DeliveredStatus);

        var deliverySummary = await deliveries
            .GroupBy(_ => 1)
            .Select(group => new DeliverySummary(
                group.Count(row => row.Status == DeliveredStatus),
                group.Count(row =>
                    row.Status == DeliveredStatus &&
                    row.EstimatedArrival != null &&
                    row.ActualArrival <= row.EstimatedArrival.Value.AddMinutes(
                        LateThresholdMinutes)),
                group.Count(row =>
                    row.Status == DeliveredStatus &&
                    row.EstimatedArrival != null &&
                    row.ActualArrival > row.EstimatedArrival.Value.AddMinutes(
                        LateThresholdMinutes))))
            .SingleOrDefaultAsync(ct);

        // A failed delivery never gets an actual_arrival (Delivery.MarkFailed only stamps
        // updated_at), so anchoring it on arrival would make every failure invisible.
        var failedCount = await db.Set<DeliveryRow>()
            .AsNoTracking()
            .CountAsync(
                row =>
                    row.Status == FailedStatus &&
                    row.UpdatedAt >= startUtcInclusive &&
                    row.UpdatedAt < endUtcExclusive,
                ct);

        var durationSummary = await (
                from delivery in delivered
                join handover in db.Set<HandoverDepartureRow>().AsNoTracking()
                    on delivery.DeliveryRouteId equals handover.DeliveryRouteId
                select new { delivery.ActualArrival, handover.DriverConfirmedAt })
            .GroupBy(_ => 1)
            .Select(group => new DurationSummary(
                group.Count(),
                group.Average(row => (double?)(
                    row.ActualArrival!.Value - row.DriverConfirmedAt).TotalMinutes)))
            .SingleOrDefaultAsync(ct);

        var deliveredRouteIds = delivered
            .Select(row => row.DeliveryRouteId)
            .Distinct();
        var utilizationPercentages = await (
                from route in db.Set<DeliveryRouteVehicleRow>().AsNoTracking()
                join outbound in db.Set<HubOutboundEventRow>().AsNoTracking()
                    on route.DeliveryRouteId equals outbound.DestinationRouteId
                where route.CapacityKg > 0m && deliveredRouteIds.Contains(route.DeliveryRouteId)
                group outbound by new { route.DeliveryRouteId, route.CapacityKg }
                into routeLoad
                select routeLoad.Sum(row => row.TotalQuantityKg) /
                    routeLoad.Key.CapacityKg * 100m)
            .ToListAsync(ct);

        return new DeliveryPerformanceReadModel(
            deliverySummary?.TotalDeliveries ?? 0,
            deliverySummary?.OnTimeCount ?? 0,
            deliverySummary?.LateCount ?? 0,
            failedCount,
            durationSummary?.AverageMinutes is null
                ? null
                : (decimal)durationSummary.AverageMinutes.Value,
            durationSummary?.SampleCount ?? 0,
            utilizationPercentages);
    }

    private sealed record DeliverySummary(
        int TotalDeliveries,
        int OnTimeCount,
        int LateCount);

    private sealed record DurationSummary(int SampleCount, double? AverageMinutes);
}
