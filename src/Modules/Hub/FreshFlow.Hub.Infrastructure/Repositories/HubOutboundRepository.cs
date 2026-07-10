using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.Repositories;

internal sealed class HubOutboundRepository(AppDbContext db) : IHubOutboundRepository
{
    public async Task AddAsync(HubOutboundEvent outbound, CancellationToken ct) =>
        await db.Set<HubOutboundEvent>().AddAsync(outbound, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    public async Task<(IReadOnlyList<HubOutboundEvent> Items, string? NextCursor, decimal TotalQuantityKg)> GetHistoryPageAsync(
        Guid hubId,
        DateOnly? date,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var query = db.Set<HubOutboundEvent>()
            .AsNoTracking()
            .Where(e => e.HubId == hubId && e.DeletedAt == null);

        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            query = query.Where(e => e.DispatchedAt >= start && e.DispatchedAt < end);
        }

        var totalQuantityKg = await query.SumAsync(e => (decimal?)e.TotalQuantityKg, ct) ?? 0m;
        var decoded = HubOutboundCursor.TryDecode(cursor);
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
            return (rows.AsReadOnly(), null, totalQuantityKg);

        var page = rows.Take(pageSize).ToList().AsReadOnly();
        return (page, HubOutboundCursor.Encode(page[^1].Id, page[^1].CreatedAt), totalQuantityKg);
    }

    private sealed record HubOutboundCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static HubOutboundCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<HubOutboundCursor>(json, Options);
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
            var json = JsonSerializer.Serialize(new HubOutboundCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
