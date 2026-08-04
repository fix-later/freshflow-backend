using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Pricing.Infrastructure.CrossModule;

/// <summary>
/// Cross-module read service: returns market products enriched with
/// product name, unit, and category from Catalog tables.
/// Cursor encodes (Id, CreatedAt) as base64 JSON ordered by (CreatedAt, Id).
/// </summary>
internal sealed class MarketProductReader(AppDbContext db) : IMarketProductReader
{
    public Task<bool> MarketExistsAsync(Guid marketId, CancellationToken ct) =>
        db.Set<MarketRow>()
            .AnyAsync(m => m.Id == marketId && m.IsActive, ct);

    public Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct) =>
        db.Set<ProductRow>()
            .AnyAsync(p => p.Id == productId, ct);

    public async Task<(IReadOnlyList<MarketProductItemDto> Items, string? NextCursor)> GetPageAsync(
        Guid marketId,
        string? category,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        // Guard: pageSize=0 would cause Take(1) to over-fetch, then rows[^1] on an empty list
        // after RemoveAt causes IndexOutOfRangeException.
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var decoded = Cursor.TryDecode(cursor);

        // ProductDetailRow already excludes deleted products via its SQL query.
        var query =
            from mp in db.Set<MarketProduct>()
            join pd in db.Set<ProductDetailRow>()
                on mp.ProductId equals pd.Id
            where mp.MarketId == marketId && mp.DeletedAt == null
            select new { mp, pd };

        if (category is not null)
            query = query.Where(x => x.pd.Category == category);

        if (decoded is not null)
        {
            var lastCreatedAt = decoded.CreatedAt;
            var lastId = decoded.Id;
            query = query.Where(x =>
                x.mp.CreatedAt > lastCreatedAt ||
                (x.mp.CreatedAt == lastCreatedAt && x.mp.Id > lastId));
        }

        query = query
            .OrderBy(x => x.mp.CreatedAt)
            .ThenBy(x => x.mp.Id);

        // Fetch pageSize + 1 to determine if a next page exists.
        // CreatedAt is projected separately because the cursor must track the sort key (CreatedAt),
        // not UpdatedAt — the two diverge whenever a price/quantity update has been applied.
        var rows = await query
            .Take(pageSize + 1)
            .Select(x => new
            {
                Dto = new MarketProductItemDto(
                    x.mp.Id,
                    x.mp.ProductId,
                    x.mp.MarketId,
                    x.pd.Name,
                    x.pd.Category,
                    x.pd.Unit,
                    x.mp.CurrentPrice,
                    x.mp.CurrentQuantity,
                    x.mp.CurrentQuantity - x.mp.ReservedQuantity,
                    x.mp.UpdatedAt,
                    x.mp.UpdatedBy,
                    new SellingUnitDto(x.pd.Unit, x.pd.CapacityKg)),
                x.mp.CreatedAt,  // sort key — must match ORDER BY clause
            })
            .ToListAsync(ct);

        string? nextCursor = null;
        if (rows.Count > pageSize)
        {
            rows.RemoveAt(rows.Count - 1);
            var last = rows[^1];
            nextCursor = Cursor.Encode(last.Dto.MarketProductId, last.CreatedAt); // ← CreatedAt, not UpdatedAt
        }

        return (rows.Select(r => r.Dto).ToList().AsReadOnly(), nextCursor);
    }

    // ── Cursor helpers ────────────────────────────────────────────────────────

    private sealed record Cursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static Cursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return null;
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<Cursor>(json, Options);
            }
            catch
            {
                return null; // invalid cursor treated as start of list
            }
        }

        public static string Encode(Guid id, DateTime createdAt)
        {
            var json = JsonSerializer.Serialize(new Cursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
