using System.Globalization;

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

    public static string PricingTags() => "pricing:tags:all";

    // ── Analytics ────────────────────────────────────────────────────────────
    // Dates are formatted invariantly: a key must not change with the host's culture.
    // Nullable filters collapse to "all" so an unfiltered query cannot collide with a
    // filtered one, and id lists are sorted so argument order never splits the entry.

    public static string AnalyticsOverview(DateOnly date) =>
        $"analytics:overview:{Day(date)}";

    public static string AnalyticsOrderMetrics(
        DateOnly from, DateOnly to, Guid? restaurantId, string? groupBy) =>
        $"analytics:order-metrics:{Day(from)}:{Day(to)}:{Filter(restaurantId)}:{Filter(groupBy)}";

    public static string AnalyticsProcurementMetrics(DateOnly from, DateOnly to, Guid? marketId) =>
        $"analytics:procurement-metrics:{Day(from)}:{Day(to)}:{Filter(marketId)}";

    public static string AnalyticsHubThroughput(DateOnly from, DateOnly to, Guid? hubId) =>
        $"analytics:hub-throughput:{Day(from)}:{Day(to)}:{Filter(hubId)}";

    public static string AnalyticsDeliveryPerformance(DateOnly from, DateOnly to) =>
        $"analytics:delivery-performance:{Day(from)}:{Day(to)}";

    public static string AnalyticsDemandHeatmap(DateOnly from, DateOnly to) =>
        $"analytics:demand-heatmap:{Day(from)}:{Day(to)}";

    public static string AnalyticsDemandTimeDistribution(DateOnly from, DateOnly to) =>
        $"analytics:demand-time-distribution:{Day(from)}:{Day(to)}";

    public static string AnalyticsPriceTrends(
        IReadOnlyList<Guid> marketProductIds, DateOnly from, DateOnly to, string? interval) =>
        $"analytics:price-trends:{Day(from)}:{Day(to)}:{Filter(interval)}:{Ids(marketProductIds)}";

    private static string Day(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Filter(Guid? id) => id?.ToString() ?? "all";

    private static string Filter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "all" : value;

    private static string Ids(IReadOnlyList<Guid> ids) =>
        ids.Count == 0 ? "none" : string.Join(",", ids.Order());
}
