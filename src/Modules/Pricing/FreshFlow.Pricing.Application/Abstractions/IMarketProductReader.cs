using FreshFlow.Pricing.Application.Dtos;

namespace FreshFlow.Pricing.Application.Abstractions;

/// <summary>
/// Cross-module read service for market product listings.
/// Joins market_products with catalog tables (products, units_of_measurement, product_categories)
/// to return enriched rows. Implemented in Infrastructure.
/// </summary>
public interface IMarketProductReader
{
    /// <summary>Returns true if the market exists and is active.</summary>
    public Task<bool> MarketExistsAsync(Guid marketId, CancellationToken ct);

    /// <summary>
    /// Returns a cursor-paginated page of active market products,
    /// enriched with productName, unit, and category from the Catalog tables.
    /// </summary>
    public Task<(IReadOnlyList<MarketProductItemDto> Items, string? NextCursor)> GetPageAsync(
        Guid marketId,
        string? category,
        string? cursor,
        int pageSize,
        CancellationToken ct);
}
