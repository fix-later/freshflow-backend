using Microsoft.Extensions.Caching.Hybrid;

namespace FreshFlow.Catalog.Application.Caching;

/// <summary>
/// Cache policy for Catalog reference data (categories, markets, units, packing codes).
///
/// These tables are read on nearly every screen and change only when an admin edits them, so
/// they are cached for <see cref="ReferenceDataTtlMinutes"/> minutes and evicted by tag the
/// moment a write commits — a reader never waits out the TTL to see an admin's change.
///
/// Keys are built by <see cref="SharedKernel.Caching.RedisKeys"/> so no two features collide
/// on a namespace; the tags below are what write handlers evict.
/// </summary>
public static class CatalogCache
{
    private const int ReferenceDataTtlMinutes = 30;

    public const string CategoriesTag = "catalog:categories";
    public const string MarketsTag = "catalog:markets";
    public const string UnitsTag = "catalog:units";
    public const string PackingCodesTag = "catalog:packing-codes";

    public static readonly HybridCacheEntryOptions ReferenceData = new()
    {
        Expiration = TimeSpan.FromMinutes(ReferenceDataTtlMinutes)
    };

    public static readonly string[] Categories = [CategoriesTag];
    public static readonly string[] Markets = [MarketsTag];
    public static readonly string[] Units = [UnitsTag];
    public static readonly string[] PackingCodes = [PackingCodesTag];
}
