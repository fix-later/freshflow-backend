using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class ConfirmedOrderReader(AppDbContext db) : IConfirmedOrderReader
{
    private const string ConfirmedStatus = "Confirmed";
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public async Task<IReadOnlyList<ConfirmedOrderDto>> ReadEligibleAsync(
        DateOnly batchDate,
        bool force,
        CancellationToken ct)
    {
        var (startUtc, endUtc) = GetUtcBounds(batchDate);
        var query = db.Set<ConfirmedOrderRow>()
            .AsNoTracking()
            .Where(row =>
                row.Status == ConfirmedStatus &&
                row.DeletedAt == null &&
                row.ScheduledFor >= startUtc &&
                row.ScheduledFor < endUtc);

        if (!force)
        {
            var coveredOrderIds = db.Set<ProcurementBatchOrder>()
                .AsNoTracking()
                .Where(link => link.DeletedAt == null)
                .Select(link => link.OrderId);
            query = query.Where(row => !coveredOrderIds.Contains(row.Id));
        }

        var orders = await query.ToListAsync(ct);
        if (orders.Count == 0)
            return [];

        var orderIds = orders.Select(order => order.Id).ToArray();
        var items = await db.Set<ConfirmedOrderItemRow>()
            .AsNoTracking()
            .Where(item => orderIds.Contains(item.OrderId))
            .ToListAsync(ct);
        var itemsByOrder = items.ToLookup(item => item.OrderId);

        return orders
            .Select(order => new ConfirmedOrderDto(
                order.Id,
                order.ScheduledFor!.Value,
                itemsByOrder[order.Id]
                    .Select(item => new ConfirmedOrderItemDto(
                        item.MarketProductId,
                        item.ProductNameSnapshot,
                        item.Quantity))
                    .ToList()
                    .AsReadOnly()))
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<ConfirmedOrderDto>> ReadEligibleForSessionAsync(
        Guid marketSessionId, CancellationToken ct)
    {
        var session = await db.Set<MarketSession>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == marketSessionId && candidate.DeletedAt == null, ct);
        var (startUtc, endUtc) = GetUtcBounds(session.ServiceDate);
        var coveredOrderIds = db.Set<ProcurementBatchOrder>()
            .AsNoTracking()
            .Where(link => link.DeletedAt == null)
            .Select(link => link.OrderId);
        var rows = await db.Set<ConfirmedOrderRow>()
            .AsNoTracking()
            .Where(row =>
                row.Status == ConfirmedStatus &&
                (row.MarketSessionId == marketSessionId ||
                 (row.MarketSessionId == null &&
                  row.MarketId == session.MarketId &&
                  row.ScheduledFor >= startUtc &&
                  row.ScheduledFor < endUtc)) &&
                row.DeletedAt == null &&
                !coveredOrderIds.Contains(row.Id))
            .ToListAsync(ct);
        if (rows.Count == 0)
            return [];

        var orderIds = rows.Select(row => row.Id).ToArray();
        var items = await db.Set<ConfirmedOrderItemRow>()
            .AsNoTracking()
            .Where(item => orderIds.Contains(item.OrderId))
            .ToListAsync(ct);
        var byOrder = items.ToLookup(item => item.OrderId);
        return rows.Select(row => new ConfirmedOrderDto(
                row.Id,
                row.ScheduledFor!.Value,
                byOrder[row.Id].Select(item => new ConfirmedOrderItemDto(
                    item.MarketProductId, item.ProductNameSnapshot, item.Quantity)).ToList()))
            .ToList();
    }

    public async Task<DateOnly?> FindOldestEligibleCycleAsync(
        DateOnly throughDate,
        CancellationToken ct)
    {
        var (_, endUtc) = GetUtcBounds(throughDate);
        var coveredOrderIds = db.Set<ProcurementBatchOrder>()
            .AsNoTracking()
            .Where(link => link.DeletedAt == null)
            .Select(link => link.OrderId);

        var scheduledFor = await db.Set<ConfirmedOrderRow>()
            .AsNoTracking()
            .Where(row =>
                row.Status == ConfirmedStatus &&
                row.DeletedAt == null &&
                row.ScheduledFor < endUtc &&
                !coveredOrderIds.Contains(row.Id))
            .OrderBy(row => row.ScheduledFor)
            .Select(row => row.ScheduledFor)
            .FirstOrDefaultAsync(ct);

        return scheduledFor.HasValue
            ? DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(scheduledFor.Value, DateTimeKind.Utc),
                    VietnamTimeZone))
            : null;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ReadStatusesAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct)
    {
        var ids = orderIds.Distinct().ToArray();

        return await db.Set<ConfirmedOrderRow>()
            .AsNoTracking()
            .Where(row => ids.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.Status, ct);
    }

    public async Task<IReadOnlyList<ConfirmedOrderItemCostDto>> ReadItemCostsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct)
    {
        var ids = orderIds.Distinct().ToArray();

        return await db.Set<ConfirmedOrderItemRow>()
            .AsNoTracking()
            .Where(row => ids.Contains(row.OrderId))
            .Select(row => new ConfirmedOrderItemCostDto(
                row.OrderId,
                row.MarketProductId,
                row.LockedTotal ?? row.Quantity * row.UnitPrice))
            .ToListAsync(ct);
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetUtcBounds(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, VietnamTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(localStart.AddDays(1), VietnamTimeZone));
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
