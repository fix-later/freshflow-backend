using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Pricing.Infrastructure.Repositories;

internal sealed class PriceSnapshotRepository(AppDbContext db) : IPriceSnapshotRepository
{
    public async Task AddAsync(PriceSnapshot snapshot, CancellationToken ct) =>
        await db.Set<PriceSnapshot>().AddAsync(snapshot, ct);

    public async Task<IReadOnlyList<PriceSnapshot>> GetByMarketProductIdAsync(
        Guid marketProductId, CancellationToken ct) =>
        await db.Set<PriceSnapshot>()
            .AsNoTracking()
            .Where(ps => ps.MarketProductId == marketProductId)
            .OrderByDescending(ps => ps.RecordedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<PriceSnapshot> Items, string? NextCursor)> GetPageAsync(
        Guid marketProductId,
        string? cursor,
        int pageSize,
        DateTime? from,
        DateTime? to,
        CancellationToken ct)
    {
        var query = db.Set<PriceSnapshot>()
            .AsNoTracking()
            .Where(ps => ps.MarketProductId == marketProductId);

        // Optional date range filter (inclusive on both ends).
        if (from.HasValue) query = query.Where(ps => ps.RecordedAt >= from.Value);
        if (to.HasValue) query = query.Where(ps => ps.RecordedAt <= to.Value);

        // Composite cursor: (RecordedAt DESC, Id DESC).
        // The predicate mirrors the ORDER BY exactly so that same-millisecond concurrent
        // writes are handled correctly:
        //   RecordedAt < cursor.RecordedAt
        //   OR (RecordedAt == cursor.RecordedAt AND Id < cursor.Id)
        var decoded = SnapshotCursor.TryDecode(cursor);
        if (decoded is not null)
            query = query.Where(ps =>
                ps.RecordedAt < decoded.RecordedAt ||
                (ps.RecordedAt == decoded.RecordedAt && ps.Id < decoded.Id));

        var rows = await query
            .OrderByDescending(ps => ps.RecordedAt)
            .ThenByDescending(ps => ps.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        if (rows.Count <= pageSize)
            return (rows.AsReadOnly(), null);

        var page = rows.Take(pageSize).ToList().AsReadOnly();
        var nextCursor = SnapshotCursor.Encode(page[^1].Id, page[^1].RecordedAt);
        return (page, nextCursor);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    // ── Cursor helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Composite cursor following the same base64-JSON pattern as <c>MarketProductReader</c>.
    /// Encodes <c>(Id, RecordedAt)</c> of the last item returned on the previous page.
    /// </summary>
    private sealed record SnapshotCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("recordedAt")] DateTime RecordedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static SnapshotCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return null;
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<SnapshotCursor>(json, Options);
            }
            catch
            {
                return null; // invalid cursor treated as start of list
            }
        }

        public static string Encode(Guid id, DateTime recordedAt)
        {
            var json = JsonSerializer.Serialize(new SnapshotCursor(id, recordedAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
