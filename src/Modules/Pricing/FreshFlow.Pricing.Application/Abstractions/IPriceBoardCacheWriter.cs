namespace FreshFlow.Pricing.Application.Abstractions;

/// <summary>
/// Writes live price data to the Redis price board hash.
///
/// Key pattern: <c>price:{marketId}:{productId}</c>
/// Fields written: <c>price</c>, <c>quantity</c>, <c>updated_at</c>, <c>updated_by</c>.
/// TTL: 5-minute absolute window, reset on every write.
///
/// The <c>reserved</c> field is intentionally excluded — it is owned by the Orders module.
/// HSET only sets the listed fields; other fields in the hash are left untouched.
///
/// Implementations propagate Redis errors; callers (event handlers) are responsible
/// for catching and logging so that Redis failures never block the price-write path.
/// </summary>
public interface IPriceBoardCacheWriter
{
    /// <summary>
    /// Writes the price snapshot for the given market-product to Redis and resets the TTL.
    /// Throws on Redis error — callers must catch to avoid blocking the write path.
    /// </summary>
    public Task WriteAsync(
        Guid marketId,
        Guid productId,
        decimal price,
        int quantity,
        DateTime updatedAt,
        Guid? updatedBy,
        CancellationToken ct = default);
}
