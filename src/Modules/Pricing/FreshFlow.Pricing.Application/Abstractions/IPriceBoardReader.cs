using FreshFlow.Pricing.Application.Dtos;

namespace FreshFlow.Pricing.Application.Abstractions;

/// <summary>
/// Reads live price/quantity data for a page of market products (UC-PRI-09).
///
/// Current implementation: <c>DbPriceBoardReader</c> — reads directly from
/// <c>market_products</c> (DB-direct, always fresh).
///
/// TODO: switch to Redis cache (IPriceBoardCache) when ready — see UC-PRI-07.
///       When adding the Redis impl, extend the input to supply both ProductId
///       (for the <c>price:{marketId}:{productId}</c> hash) and MarketProductId
///       (for the <c>order:reservation:{marketProductId}</c> counter).
///
/// Implementations MUST NOT throw — callers treat exceptions as cache failures
/// and fall back to the DB values already loaded by <c>IMarketProductReader</c>.
/// </summary>
public interface IPriceBoardReader
{
    /// <summary>
    /// Returns live price entries keyed by <paramref name="productIds"/>.
    /// Missing keys in the result = cache/DB miss → caller falls back to existing DB values.
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, LivePriceEntry>> GetBatchAsync(
        Guid marketId,
        IReadOnlyList<Guid> productIds,
        CancellationToken ct = default);
}
