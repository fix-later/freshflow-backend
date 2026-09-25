using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.SharedKernel.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Catalog;

/// <summary>
/// Phase 2 gate for the Redis cache layer (docs/features/caching/PLAN-2026-09-25-redis-cache-layer.md).
///
/// Catalog reference data is cached for 30 minutes, so the risk this guards is an admin edit that
/// readers cannot see until the TTL expires. These tests run the full host — real DI, real
/// HybridCache, real Postgres — and assert that a write is visible on the very next read.
///
/// A unit test cannot cover this: the eviction has to travel from the command handler through the
/// same cache instance the query handler reads from, which only the composed host proves.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CatalogCacheInvalidationTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record NamedItem(Guid Id, string Name);

    private async Task AuthorizeAsAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "admin@test.freshflow",
            password = "AdminP@ss1"
        });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", env!.Data!.AccessToken);
    }

    private async Task<IReadOnlyList<NamedItem>> GetMarketsAsync()
    {
        var resp = await _client.GetAsync("/api/v1/markets");
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<List<NamedItem>>>();
        return env!.Data!;
    }

    private async Task<IReadOnlyList<NamedItem>> GetUnitsAsync()
    {
        var resp = await _client.GetAsync("/api/v1/units");
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<List<NamedItem>>>();
        return env!.Data!;
    }

    [Fact]
    public async Task A_created_market_is_visible_on_the_next_read_not_after_the_TTL()
    {
        // Arrange — the first read warms the cache, so a stale entry now exists to be invalidated
        await AuthorizeAsAdminAsync();
        var before = await GetMarketsAsync();
        var name = $"Cache Invalidation Market {Guid.NewGuid():N}";

        // Act
        var created = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name,
            location = "Test Location",
            address = "123 Test Street",
            latitude = (decimal?)10.123,
            longitude = (decimal?)106.456
        });
        created.EnsureSuccessStatusCode();
        var after = await GetMarketsAsync();

        // Assert
        before.Should().NotContain(m => m.Name == name);
        after.Should().Contain(m => m.Name == name, "creating a market must evict the markets cache tag");
    }

    [Fact]
    public async Task A_created_unit_is_visible_on_the_next_read_not_after_the_TTL()
    {
        // Arrange
        await AuthorizeAsAdminAsync();
        var before = await GetUnitsAsync();
        var name = $"unit-{Guid.NewGuid():N}"[..20];

        // Act
        var created = await _client.PostAsJsonAsync("/api/v1/units", new { name, abbreviation = "u" });
        created.EnsureSuccessStatusCode();
        var after = await GetUnitsAsync();

        // Assert
        before.Should().NotContain(u => u.Name == name);
        after.Should().Contain(u => u.Name == name, "creating a unit must evict the units cache tag");
    }

    [Fact]
    public async Task Evicting_one_family_does_not_drop_another_familys_cached_data()
    {
        // Arrange — both families cached
        await AuthorizeAsAdminAsync();
        await GetMarketsAsync();
        var unitsBefore = await GetUnitsAsync();

        // Act — a markets write must not disturb the units cache
        var created = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name = $"Scoped Evict Market {Guid.NewGuid():N}",
            location = "Test Location",
            address = "123 Test Street"
        });
        created.EnsureSuccessStatusCode();

        // Assert
        var unitsAfter = await GetUnitsAsync();
        unitsAfter.Select(u => u.Name).Should().BeEquivalentTo(unitsBefore.Select(u => u.Name));
    }

    [Fact]
    public async Task A_read_actually_populates_the_cache()
    {
        // Without this, the eviction tests above would still pass with caching switched off —
        // they would just be reading the database twice. This pins down that GET /api/v1/markets
        // really does leave an entry behind, which is what the other tests then invalidate.
        await AuthorizeAsAdminAsync();
        await GetMarketsAsync();

        var cache = factory.Services.GetRequiredService<HybridCache>();
        var factoryRan = false;

        // A hit serves the stored value and never calls this factory. The type argument must
        // match what the handler cached (IReadOnlyList<MarketDto>) — HybridCache entries are
        // typed, so probing the same key with a different shape reads as a miss.
        await cache.GetOrCreateAsync(
            RedisKeys.CatalogMarkets(activeOnly: true),
            _ =>
            {
                factoryRan = true;
                return ValueTask.FromResult<IReadOnlyList<MarketDto>>([]);
            });

        factoryRan.Should().BeFalse("GET /api/v1/markets should have left the list in the cache");
    }
}
