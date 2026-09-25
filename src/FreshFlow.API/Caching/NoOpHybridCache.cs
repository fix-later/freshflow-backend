using Microsoft.Extensions.Caching.Hybrid;

namespace FreshFlow.API.Caching;

/// <summary>
/// <see cref="HybridCache"/> substitute used when <c>Cache:Enabled</c> is <c>false</c>.
/// Every call bypasses caching entirely (factory runs unconditionally, writes/evictions
/// are no-ops) so Phase 2/3 call sites can call <see cref="HybridCache"/> unconditionally
/// while still being able to fully disable caching for debugging.
/// </summary>
internal sealed class NoOpHybridCache : HybridCache
{
    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        factory(state, cancellationToken);

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
