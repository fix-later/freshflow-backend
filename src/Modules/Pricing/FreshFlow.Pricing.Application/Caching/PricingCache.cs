using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Pricing.Application.Caching;

/// <summary>
/// Cache for the catalog tag list — the one Pricing read that is reference data rather than live
/// prices. The price board keeps its own Redis mechanism (<c>RedisPriceBoardCache</c>) and is not
/// affected by this.
///
/// Deliberately concrete instead of a generic cache wrapper: Pricing has exactly one cached query
/// and one tag, so the key and the entry options belong here rather than at the call site.
///
/// <see cref="EvictTagsAsync"/> swallows its failure on purpose. Reads through
/// <see cref="HybridCache"/> already degrade to the factory when the backing store is down, but
/// <see cref="HybridCache.RemoveByTagAsync"/> propagates instead. Letting that escape a command
/// handler would answer 500 for a write that already committed, telling an admin their edit failed
/// when it succeeded. A failed eviction only means readers see a stale tag list until the TTL runs
/// out. Same reasoning as <c>CatalogCacheEvictor</c>; duplicated per module rather than shared,
/// because a shared home would have to be SharedKernel, which must not take a Redis dependency
/// (see the dependency rules in CLAUDE.md).
/// </summary>
public sealed class PricingCache(HybridCache cache, ILogger<PricingCache> logger)
{
    private const int TagsTtlMinutes = 30;
    private const string TagsCacheTag = "pricing:tags";

    private static readonly string[] TagsCacheTags = [TagsCacheTag];

    private static readonly HybridCacheEntryOptions TagsEntry = new()
    {
        Expiration = TimeSpan.FromMinutes(TagsTtlMinutes)
    };

    public ValueTask<IReadOnlyList<TagDto>> GetTagsAsync(
        Func<CancellationToken, ValueTask<IReadOnlyList<TagDto>>> factory,
        CancellationToken ct = default) =>
        cache.GetOrCreateAsync(RedisKeys.PricingTags(), factory, TagsEntry, TagsCacheTags, ct);

    public async Task EvictTagsAsync(CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveByTagAsync(TagsCacheTag, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Tag list cache eviction failed; entries stay until their TTL expires");
        }
    }
}
