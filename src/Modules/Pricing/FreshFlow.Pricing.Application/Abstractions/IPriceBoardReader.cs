using FreshFlow.Pricing.Application.Dtos;

namespace FreshFlow.Pricing.Application.Abstractions;

/// <summary>
/// Reads live price/quantity data for a page of market products (UC-PRI-09).
///
/// The caller keeps DB values for cache misses and falls back to them when Redis fails.
/// </summary>
public interface IPriceBoardReader
{
    /// <summary>
    /// Returns live price entries keyed by <paramref name="productIds"/>.
    /// Missing keys in the result = cache miss → caller falls back to existing DB values.
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, LivePriceEntry>> GetBatchAsync(
        Guid marketId,
        IReadOnlyList<Guid> productIds,
        CancellationToken ct = default);
}
