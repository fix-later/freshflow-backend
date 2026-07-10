using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.Repositories;

internal sealed class HubInboundRepository(AppDbContext db) : IHubInboundRepository
{
    public async Task AddAsync(HubInboundEvent inbound, CancellationToken ct) =>
        await db.Set<HubInboundEvent>().AddAsync(inbound, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    public Task<HubInboundEvent?> FindPendingByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<HubInboundEvent>()
            .FirstOrDefaultAsync(e =>
                e.Id == id &&
                e.Status == HubInboundEvent.StatusPending &&
                e.DeletedAt == null,
                ct);

    public Task<bool> DeliveryScheduleExistsAsync(Guid hubId, Guid deliveryScheduleId, CancellationToken ct) =>
        db.Set<HubInboundEvent>()
            .AsNoTracking()
            .AnyAsync(e =>
                e.HubId == hubId &&
                e.DeliveryScheduleId == deliveryScheduleId &&
                e.DeletedAt == null,
                ct);

    public async Task<(IReadOnlyList<HubInboundEvent> Items, string? NextCursor)> GetPendingPageAsync(
        Guid hubId,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var query = db.Set<HubInboundEvent>()
            .AsNoTracking()
            .Where(e =>
                e.HubId == hubId &&
                e.DeletedAt == null &&
                (e.Status == HubInboundEvent.StatusPending ||
                    e.Status == HubInboundEvent.StatusArrivedAtHub));

        var (items, nextCursor) = await ApplyCursorAsync(query, cursor, pageSize, ct);
        return (items, nextCursor);
    }

    public async Task<(IReadOnlyList<HubInboundEvent> Items, string? NextCursor, decimal TotalQuantityKg)> GetHistoryPageAsync(
        Guid hubId,
        DateOnly? date,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var query = db.Set<HubInboundEvent>()
            .AsNoTracking()
            .Where(e => e.HubId == hubId && e.DeletedAt == null);

        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            query = query.Where(e => e.ArrivedAt >= start && e.ArrivedAt < end);
        }

        var totalQuantityKg = await query.SumAsync(e => (decimal?)e.TotalQuantityKg, ct) ?? 0m;
        var (items, nextCursor) = await ApplyCursorAsync(query, cursor, pageSize, ct);
        return (items, nextCursor, totalQuantityKg);
    }

    private static async Task<(IReadOnlyList<HubInboundEvent> Items, string? NextCursor)> ApplyCursorAsync(
        IQueryable<HubInboundEvent> query,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        var decoded = HubInboundCursor.TryDecode(cursor);
        if (decoded is not null)
        {
            query = query.Where(e =>
                e.CreatedAt < decoded.CreatedAt ||
                (e.CreatedAt == decoded.CreatedAt && e.Id < decoded.Id));
        }

        var rows = await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        if (rows.Count <= pageSize)
            return (rows.AsReadOnly(), null);

        var page = rows.Take(pageSize).ToList().AsReadOnly();
        var nextCursor = HubInboundCursor.Encode(page[^1].Id, page[^1].CreatedAt);
        return (page, nextCursor);
    }

    private sealed record HubInboundCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static HubInboundCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<HubInboundCursor>(json, Options);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static string Encode(Guid id, DateTime createdAt)
        {
            var json = JsonSerializer.Serialize(new HubInboundCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
