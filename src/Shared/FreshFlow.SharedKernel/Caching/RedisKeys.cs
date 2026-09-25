namespace FreshFlow.SharedKernel.Caching;

/// <summary>
/// Single place to build Redis cache keys, so no two callers accidentally collide on the
/// same namespace. Add a new builder here (not ad hoc string interpolation at the call site)
/// when a new cache use lands.
/// </summary>
public static class RedisKeys
{
    public static string Price(Guid marketId, Guid productId) =>
        $"price:{marketId}:{productId}";

    public static string CatalogCategories(bool activeOnly) =>
        $"catalog:categories:{activeOnly}";

    public static string CatalogMarkets(bool activeOnly) =>
        $"catalog:markets:{activeOnly}";

    public static string CatalogUnits(bool activeOnly) =>
        $"catalog:units:{activeOnly}";

    public static string CatalogPackingCodes(bool activeOnly, int page, int pageSize) =>
        $"catalog:packing-codes:{activeOnly}:{page}:{pageSize}";
}
