using FreshFlow.Pricing.Application.Queries.SearchMarketProducts;
using FreshFlow.Pricing.Domain.Entities;

namespace FreshFlow.Pricing.Application.Abstractions;

public interface IMarketProductRepository
{
    public Task<MarketProduct?> FindByIdAsync(Guid id, CancellationToken ct);

    public Task<MarketProduct?> FindByMarketAndProductAsync(
        Guid marketId, Guid productId, CancellationToken ct);

    public Task<IReadOnlyList<MarketProduct>> GetByMarketIdAsync(
        Guid marketId, CancellationToken ct);

    /// <summary>
    /// Free-text search by product name, scoped to a market. Joins Catalog's
    /// products table (active, not soft-deleted) and projects price/availability
    /// from this module's market_products in a single round-trip. No N+1.
    /// </summary>
    public Task<(IReadOnlyList<MarketProductSearchItemDto> Items, string? NextCursor)> SearchAsync(
        MarketProductSearchCriteria criteria, CancellationToken ct);

    public Task AddAsync(MarketProduct marketProduct, CancellationToken ct);

    /// <summary>
    /// Atomically inserts <paramref name="marketProduct"/> and saves changes.
    /// Throws <see cref="DuplicateMarketProductException"/> if a concurrent insert
    /// races the pre-check and violates the (market_id, product_id) unique index.
    /// </summary>
    public Task AddAndSaveAsync(MarketProduct marketProduct, CancellationToken ct);

    /// <summary>
    /// Re-attaches a detached entity to the change tracker so mutations are persisted.
    /// </summary>
    public void Track(MarketProduct marketProduct);

    public Task SaveChangesAsync(CancellationToken ct);
}

public sealed record MarketProductSearchCriteria(
    Guid MarketId,
    string SearchText,
    string? Category,
    bool InStockOnly,
    string? Cursor,
    int PageSize);
