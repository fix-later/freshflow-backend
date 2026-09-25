using FreshFlow.Analytics.Application.Common;
using Microsoft.Extensions.Caching.Hybrid;

namespace FreshFlow.Analytics.Application.Caching;

/// <summary>
/// Cache policy for the Analytics dashboards.
///
/// Analytics is read-only and aggregates over large tables, so it is cached on TTL alone — no
/// eviction. The TTL is tiered by whether the requested range can still change:
///
/// <list type="bullet">
/// <item>a range that ends before today (Vietnam time) is settled history and cannot change, so
/// it is held for <see cref="ClosedRangeTtlHours"/> hours;</item>
/// <item>a range touching today is still accumulating orders and deliveries, so it is held for
/// only <see cref="OpenRangeTtlMinutes"/> minutes.</item>
/// </list>
///
/// "Today" is a Vietnam business date, not a UTC one. A cache key built from an unresolved
/// "today" would keep serving yesterday's numbers past midnight in Vietnam until the TTL ran
/// out, so callers must resolve the date (see <see cref="Today"/>) and key on the result.
/// </summary>
public sealed class AnalyticsCache(HybridCache cache, TimeProvider timeProvider)
{
    private const int OpenRangeTtlMinutes = 5;
    private const int ClosedRangeTtlHours = 6;

    private static readonly HybridCacheEntryOptions OpenRange = new()
    {
        Expiration = TimeSpan.FromMinutes(OpenRangeTtlMinutes)
    };

    private static readonly HybridCacheEntryOptions ClosedRange = new()
    {
        Expiration = TimeSpan.FromHours(ClosedRangeTtlHours)
    };

    /// <summary>Today as a Vietnam business date.</summary>
    public DateOnly Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), VietnamTime.Zone).DateTime);

    /// <param name="rangeEnd">Last day the query covers — decides which TTL tier applies.</param>
    public ValueTask<T> GetOrCreateAsync<T>(
        string key,
        DateOnly rangeEnd,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken ct = default) =>
        cache.GetOrCreateAsync(key, factory, OptionsFor(rangeEnd), cancellationToken: ct);

    internal HybridCacheEntryOptions OptionsFor(DateOnly rangeEnd) =>
        rangeEnd < Today ? ClosedRange : OpenRange;
}
