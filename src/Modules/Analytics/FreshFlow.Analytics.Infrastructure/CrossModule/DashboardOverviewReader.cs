using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class DashboardOverviewReader(AppDbContext db) : IDashboardOverviewReader
{
    private const string OrderStatusDraft = "Draft";
    private const string OrderStatusConfirmed = "Confirmed";
    private const string OrderStatusCancelled = "Cancelled";
    private const string ProcurementStatusCompleted = "HandedOff";
    private const string ProcurementStatusCancelled = "Cancelled";
    private const string DeliveryStatusDelivered = "delivered";
    private const string HubInboundStatusArrived = "ARRIVED_AT_HUB";

    public async Task<DashboardOverviewReadModel> ReadAsync(
        DateOnly date,
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct)
    {
        var orders = await db.Set<OrderSummaryRow>()
            .AsNoTracking()
            .Where(row => row.CreatedAt >= startUtcInclusive && row.CreatedAt < endUtcExclusive)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Revenue = group.Sum(row =>
                    row.Status == OrderStatusCancelled || row.Status == OrderStatusDraft
                        ? 0m
                        : row.TotalAmount),
                Pending = group.Count(row =>
                    row.Status == OrderStatusDraft || row.Status == OrderStatusConfirmed)
            })
            .SingleOrDefaultAsync(ct);

        var cancelledToday = await db.Set<OrderSummaryRow>()
            .AsNoTracking()
            .CountAsync(row =>
                row.Status == OrderStatusCancelled &&
                row.CancelledAt >= startUtcInclusive &&
                row.CancelledAt < endUtcExclusive,
                ct);

        var activeProcurementBatches = await db.Set<ProcurementBatchRow>()
            .AsNoTracking()
            .CountAsync(row =>
                row.BatchDate == date &&
                row.Status != ProcurementStatusCompleted &&
                row.Status != ProcurementStatusCancelled,
                ct);

        var deliveries = await db.Set<DeliveryRow>()
            .AsNoTracking()
            .Where(row =>
                row.Status == DeliveryStatusDelivered &&
                row.ActualArrival >= startUtcInclusive &&
                row.ActualArrival < endUtcExclusive)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                OnTime = group.Count(row =>
                    row.EstimatedArrival != null &&
                    row.ActualArrival <= row.EstimatedArrival.Value.AddMinutes(15))
            })
            .SingleOrDefaultAsync(ct);

        var inboundKg = await db.Set<HubInboundEventRow>()
            .AsNoTracking()
            .Where(row =>
                row.Status == HubInboundStatusArrived &&
                row.ArrivedAt >= startUtcInclusive &&
                row.ArrivedAt < endUtcExclusive)
            .SumAsync(row => (decimal?)row.TotalQuantityKg, ct) ?? 0m;

        var outboundKg = await db.Set<HubOutboundEventRow>()
            .AsNoTracking()
            .Where(row =>
                row.DispatchedAt >= startUtcInclusive &&
                row.DispatchedAt < endUtcExclusive)
            .SumAsync(row => (decimal?)row.TotalQuantityKg, ct) ?? 0m;

        return new DashboardOverviewReadModel(
            orders?.Count ?? 0,
            orders?.Revenue ?? 0m,
            orders?.Pending ?? 0,
            cancelledToday,
            activeProcurementBatches,
            deliveries?.Count ?? 0,
            deliveries?.OnTime ?? 0,
            inboundKg,
            outboundKg);
    }
}

