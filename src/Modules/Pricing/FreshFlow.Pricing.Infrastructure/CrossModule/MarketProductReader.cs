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
        string? tag,
        CancellationToken ct)
    {
        // Guard: pageSize=0 would cause Take(1) to over-fetch, then rows[^1] on an empty list
        // after RemoveAt causes IndexOutOfRangeException.
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var decoded = Cursor.TryDecode(cursor);

        // ProductDetailRow already excludes deleted products via its SQL query.
        var baseQuery =
            from mp in db.Set<MarketProduct>()
            join pd in db.Set<ProductDetailRow>()
                on mp.ProductId equals pd.Id
            where mp.MarketId == marketId && mp.DeletedAt == null
            select new { mp, pd };

        if (category is not null)
            baseQuery = baseQuery.Where(x => x.pd.Category == category);

        // Stored tag names are normalized (trim + lowercase-invariant) on write, so the filter
        // input must be normalized the same way or a mixed-case ?tag= silently matches nothing.
        // Soft-deleted tags are already excluded by Tag's global query filter.
        if (tag is not null)
        {
            var normalizedTag = tag.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(x => x.mp.Tags.Any(t => t.Name == normalizedTag));
        }

        // Featured items (carrying any tag with PinsToTop) are pinned to the top of page 1 only
        // (cursor == null). The keyset stream below always excludes featured items, so nothing
        // repeats across pages and the cursor logic stays unchanged.
        // ponytail: assumes few featured items per market — they all land on page 1.
        IReadOnlyList<MarketProductItemDto> featured = [];
        if (decoded is null)
        {
            featured = await baseQuery
                .Where(x => x.mp.Tags.Any(t => t.PinsToTop))
                .OrderBy(x => x.mp.CreatedAt)
                .ThenBy(x => x.mp.Id)
                .Select(x => new MarketProductItemDto(
                    x.mp.Id,
                    x.mp.ProductId,
                    x.mp.MarketId,
                    x.pd.Name,
                    x.pd.ImageUrl,
                    x.pd.Category,
                    x.pd.Unit,
                    x.mp.CurrentPrice,
                    x.mp.CurrentQuantity,
                    x.mp.CurrentQuantity - x.mp.ReservedQuantity,
                    Array.Empty<MarketProductTagDto>(), // Tags filled in by LoadTagsAsync below — see its doc comment for why.
                    x.mp.UpdatedAt,
                    x.mp.UpdatedBy,
                    new SellingUnitDto(x.pd.Unit, x.pd.CapacityKg)))
                .ToListAsync(ct);
        }

        var query = baseQuery.Where(x => !x.mp.Tags.Any(t => t.PinsToTop));

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
                    x.pd.ImageUrl,
                    x.pd.Category,
                    x.pd.Unit,
                    x.mp.CurrentPrice,
                    x.mp.CurrentQuantity,
                    x.mp.CurrentQuantity - x.mp.ReservedQuantity,
                    Array.Empty<MarketProductTagDto>(), // Tags filled in by LoadTagsAsync below — see its doc comment for why.
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

        var pageItems = rows.Select(r => r.Dto);
        var items = (decoded is null ? featured.Concat(pageItems) : pageItems).ToList();
        var tagsByMarketProductId = await LoadTagsAsync(items.Select(i => i.MarketProductId), ct);
        items = items
            .Select(i => i with
            {
                Tags = tagsByMarketProductId.GetValueOrDefault(
                    i.MarketProductId, (IReadOnlyList<MarketProductTagDto>)[])
            })
            .ToList();

        return (items.AsReadOnly(), nextCursor);
    }

    /// <summary>
    /// Loads tags for a set of market products in one round-trip, keyed by MarketProductId.
    /// Deliberately a separate query rooted at the keyed <c>MarketProduct</c> entity (not
    /// projected inline above): EF Core cannot translate a collection subquery inside a
    /// projection that also joins a keyless entity (<see cref="ProductDetailRow"/>) — "Unable to
    /// translate a collection subquery in a projection... This can happen when trying to
    /// correlate on keyless entity type."
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<MarketProductTagDto>>> LoadTagsAsync(
        IEnumerable<Guid> marketProductIds, CancellationToken ct)
    {
        var ids = marketProductIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var rows = await db.Set<MarketProduct>()
            .Where(mp => ids.Contains(mp.Id))
            .Select(mp => new
            {
                mp.Id,
                Tags = mp.Tags.Select(t => new MarketProductTagDto(t.Id, t.Name, t.PinsToTop)).ToList(),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => (IReadOnlyList<MarketProductTagDto>)r.Tags);
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
