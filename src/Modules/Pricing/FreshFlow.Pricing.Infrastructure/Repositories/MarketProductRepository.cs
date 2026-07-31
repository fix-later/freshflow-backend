using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Queries.SearchMarketProducts;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Pricing.Infrastructure.CrossModule;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Pricing.Infrastructure.Repositories;

internal sealed class MarketProductRepository(AppDbContext db) : IMarketProductRepository
{
    public Task<MarketProduct?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<MarketProduct>()
            .AsNoTracking()
            .FirstOrDefaultAsync(mp => mp.Id == id && mp.DeletedAt == null, ct);

    public Task<MarketProduct?> FindByMarketAndProductAsync(
        Guid marketId, Guid productId, CancellationToken ct) =>
        db.Set<MarketProduct>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                mp => mp.MarketId == marketId
                      && mp.ProductId == productId
                      && mp.DeletedAt == null,
                ct);

    public async Task<IReadOnlyList<MarketProduct>> GetByMarketIdAsync(
        Guid marketId, CancellationToken ct) =>
        await db.Set<MarketProduct>()
            .AsNoTracking()
            .Where(mp => mp.MarketId == marketId && mp.DeletedAt == null)
            .OrderBy(mp => mp.ProductId)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<MarketProductSearchItemDto> Items, string? NextCursor)> SearchAsync(
        MarketProductSearchCriteria criteria, CancellationToken ct)
    {
        if (criteria.PageSize <= 0)
            throw new ArgumentException("PageSize must be greater than zero.", nameof(criteria));

        var decoded = SearchCursor.TryDecode(criteria.Cursor);

        // ProductDetailRow already excludes soft-deleted products via its SQL query.
        var pattern = "%" + EscapeILikePattern(criteria.SearchText) + "%";
        var query =
            from mp in db.Set<MarketProduct>()
            join pd in db.Set<ProductDetailRow>()
                on mp.ProductId equals pd.Id
            where mp.MarketId == criteria.MarketId
                  && mp.DeletedAt == null
                  && EF.Functions.ILike(pd.Name, pattern, "\\")
            select new { mp, pd };

        if (criteria.Category is not null)
            query = query.Where(x => x.pd.Category == criteria.Category);

        if (criteria.InStockOnly)
            query = query.Where(x => x.mp.CurrentQuantity - x.mp.ReservedQuantity > 0);

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
        var rows = await query
            .Take(criteria.PageSize + 1)
            .Select(x => new
            {
                Dto = new MarketProductSearchItemDto(
                    x.mp.Id,
                    x.mp.ProductId,
                    x.pd.Name,
                    x.pd.Category,
                    x.mp.CurrentPrice,
                    x.mp.CurrentQuantity - x.mp.ReservedQuantity),
                x.mp.CreatedAt,  // sort key — must match ORDER BY clause
            })
            .ToListAsync(ct);

        string? nextCursor = null;
        if (rows.Count > criteria.PageSize)
        {
            rows.RemoveAt(rows.Count - 1);
            var last = rows[^1];
            nextCursor = SearchCursor.Encode(last.Dto.MarketProductId, last.CreatedAt);
        }

        return (rows.Select(r => r.Dto).ToList().AsReadOnly(), nextCursor);
    }

    public async Task AddAsync(MarketProduct marketProduct, CancellationToken ct) =>
        await db.Set<MarketProduct>().AddAsync(marketProduct, ct);

    public async Task AddAndSaveAsync(MarketProduct marketProduct, CancellationToken ct)
    {
        await db.Set<MarketProduct>().AddAsync(marketProduct, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            throw new DuplicateMarketProductException(
                "This product is already listed at this market.", ex);
        }
    }

    public void Track(MarketProduct marketProduct)
    {
        db.Update(marketProduct);
        db.Entry(marketProduct).Property(product => product.ReservedQuantity).IsModified = false;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "The record was updated by another user. Please refresh and retry.", ex);
        }
    }

    /// <summary>
    /// Escapes PostgreSQL ILIKE wildcard characters in a user-supplied search term so they
    /// are treated as literal characters rather than wildcards. Backslash is escaped first
    /// to avoid double-escaping. The escape character is declared as <c>\</c> in the ILike call.
    /// </summary>
    internal static string EscapeILikePattern(string value) =>
        value.Replace("\\", "\\\\")
             .Replace("%", "\\%")
             .Replace("_", "\\_");

    // ── Cursor helpers ────────────────────────────────────────────────────────

    private sealed record SearchCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static SearchCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return null;
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<SearchCursor>(json, Options);
            }
            catch (FormatException)
            {
                return null; // invalid Base64 — treat as start of list
            }
            catch (JsonException)
            {
                return null; // invalid JSON payload — treat as start of list
            }
        }

        public static string Encode(Guid id, DateTime createdAt)
        {
            var json = JsonSerializer.Serialize(new SearchCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
