using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace FreshFlow.IntegrationTests.Benchmarks;

/// <summary>
/// Measures what the Analytics cache actually buys, to decide whether Phase 4 of
/// docs/features/caching/PLAN-2026-09-25-redis-cache-layer.md is worth doing.
///
/// Skipped on purpose: it seeds tens of thousands of rows and asserts on wall-clock time, which
/// belongs in neither CI nor a normal test run. Remove the Skip and run it alone to re-measure:
///
///   dotnet test tests/Integration/FreshFlow.IntegrationTests/ \
///     --filter "FullyQualifiedName~AnalyticsCacheBenchmark" -- \
///     xunit.diagnosticMessages=true
///
/// Measured 2026-09-26 on Postgres 16 in Testcontainers, one restaurant, orders spread over a
/// 90-day range, grouped by day:
///
/// <code>
///   20,000 orders :  19.7 ms uncached ->  0.9 ms cached  (22.8x)
///  100,000 orders :  85.8 ms uncached ->  0.9 ms cached (100.8x)
/// </code>
///
/// Uncached cost grows roughly linearly with row count; cached cost is flat at about 1 ms. That is
/// the shape that decides where caching pays — full-table aggregates, not bounded per-entity reads.
///
/// Method: a warm-up pass absorbs JIT and EF model building. Uncached cost is then measured with a
/// distinct date range per call so every call is a cache miss; cached cost repeats one range and
/// discards the first (miss) call. Medians are reported, not means, so one stray pause cannot move
/// the number.
/// </summary>
[Trait("Category", "Benchmark")]
public sealed class AnalyticsCacheBenchmark(AuthWebAppFactory factory, ITestOutputHelper output)
    : IClassFixture<AuthWebAppFactory>
{
    private const int SeededOrders = 20_000;
    private const int Samples = 5;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(Skip = "Benchmark — run explicitly, see the class comment")]
    public async Task Measures_order_metrics_cached_versus_uncached()
    {
        await AuthenticateAsAdminAsync();
        var restaurantId = await CreateRestaurantAsync();
        await SeedOrdersAsync(restaurantId);

        // Warm-up: JIT, EF model, connection pool — not part of either measurement.
        for (var i = 0; i < 2; i++)
            await TimeAsync(Url(restaurantId, "2025-01-01", "2025-01-02"));

        // Uncached: a different range every call, so each one misses and hits Postgres.
        var uncached = new List<double>();
        for (var i = 0; i < Samples; i++)
            uncached.Add(await TimeAsync(Url(restaurantId, "2026-04-01", $"2026-06-{10 + i:D2}")));

        // Cached: one range repeated; the first call is the miss that populates it.
        var cachedUrl = Url(restaurantId, "2026-04-01", "2026-06-30");
        await TimeAsync(cachedUrl);
        var cached = new List<double>();
        for (var i = 0; i < Samples; i++)
            cached.Add(await TimeAsync(cachedUrl));

        var uncachedMedian = Median(uncached);
        var cachedMedian = Median(cached);

        output.WriteLine($"orders seeded      : {SeededOrders:N0}");
        output.WriteLine($"uncached (median)  : {uncachedMedian:F1} ms   [{Format(uncached)}]");
        output.WriteLine($"cached   (median)  : {cachedMedian:F1} ms   [{Format(cached)}]");
        output.WriteLine($"saved per hit      : {uncachedMedian - cachedMedian:F1} ms "
                         + $"({uncachedMedian / Math.Max(cachedMedian, 0.01):F1}x faster)");
    }

    private static string Url(Guid restaurantId, string from, string to) =>
        $"/api/v1/analytics/order-metrics?from={from}&to={to}&restaurantId={restaurantId}&groupBy=day";

    private async Task<double> TimeAsync(string url)
    {
        var sw = Stopwatch.StartNew();
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsStringAsync();
        return sw.Elapsed.TotalMilliseconds;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }

    private static string Format(IEnumerable<double> values) =>
        string.Join(", ", values.Select(v => $"{v:F0}"));

    private async Task SeedOrdersAsync(Guid restaurantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var batch = 0; batch < SeededOrders / 1_000; batch++)
        {
            for (var i = 0; i < 1_000; i++)
            {
                var order = new Order(restaurantId, null, null);
                order.ClearDomainEvents();
                db.Set<Order>().Add(order);
            }

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        // One set-based update spreads the orders over a quarter and gives them money and statuses.
        // Column casing follows the orders table exactly: quoted PascalCase, snake_case deleted_at.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE orders
            SET "CreatedAt" = timestamptz '2026-04-01 00:00:00+07'
                              + (random() * 90 * interval '1 day'),
                "TotalAmount" = (100000 + random() * 900000)::numeric(18, 2),
                "Status" = (ARRAY['Confirmed', 'Delivered', 'Cancelled'])[1 + floor(random() * 3)]
            WHERE "RestaurantId" = {restaurantId}
            """);
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "admin@test.freshflow",
            password = "AdminP@ss1"
        });
        response.EnsureSuccessStatusCode();
        var env = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", env!.Data!.AccessToken);
    }

    private async Task<Guid> CreateRestaurantAsync()
    {
        var email = $"cache-benchmark-{Guid.NewGuid():N}@test.freshflow";
        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password = "RestaurantP@ss1",
            role = "restaurant",
            restaurantName = "Cache Benchmark Restaurant"
        });
        create.EnsureSuccessStatusCode();

        var list = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        list.EnsureSuccessStatusCode();
        var body = await list.Content.ReadFromJsonAsync<Envelope<BenchmarkUserList>>();
        return body!.Data!.Data.Single().RestaurantId!.Value;
    }

    private sealed record BenchmarkUserList(List<BenchmarkUserRow> Data);

    private sealed record BenchmarkUserRow(Guid Id, Guid? RestaurantId);
}
