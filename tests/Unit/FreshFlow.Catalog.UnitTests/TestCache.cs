using FreshFlow.Catalog.Application.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreshFlow.Catalog.UnitTests;

/// <summary>
/// Real <see cref="HybridCache"/> instances for handler tests — L1 only, no Redis. A real cache
/// rather than a mock, because what these tests need to assert is the caching itself (a second
/// call must not reach the repository, an eviction must let it through again).
///
/// xUnit builds a fresh test-class instance per test, so each test gets its own empty cache.
/// </summary>
internal static class TestCache
{
    public static HybridCache Create() =>
        new ServiceCollection()
            .AddHybridCache().Services
            .BuildServiceProvider()
            .GetRequiredService<HybridCache>();

    /// <summary>A cache whose backing store fails every call — models Redis being down.</summary>
    public static HybridCache CreateWithFailingBackend() =>
        new ServiceCollection()
            .AddSingleton<IDistributedCache, FailingDistributedCache>()
            .AddHybridCache().Services
            .BuildServiceProvider()
            .GetRequiredService<HybridCache>();

    public static CatalogCacheEvictor Evictor(HybridCache? cache = null) =>
        new(cache ?? Create(), NullLogger<CatalogCacheEvictor>.Instance);

    private sealed class FailingDistributedCache : IDistributedCache
    {
        private static InvalidOperationException Down() => new("cache backend is down");

        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(
            string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default) => throw Down();
    }
}
