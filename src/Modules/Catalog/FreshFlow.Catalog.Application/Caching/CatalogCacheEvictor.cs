using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Catalog.Application.Caching;

/// <summary>
/// Evicts a Catalog cache tag after a write commits.
///
/// Swallowing the failure is deliberate and is the whole reason this sits behind a helper
/// rather than being called inline: unlike reads, <see cref="HybridCache.RemoveByTagAsync"/>
/// propagates the backing-store exception. Letting it escape a command handler would return 500
/// for a write that already committed — the admin would be told their edit failed when it
/// actually succeeded. A failed eviction only means readers see stale reference data until the
/// TTL expires, which is strictly better than lying about the write.
/// </summary>
public sealed class CatalogCacheEvictor(HybridCache cache, ILogger<CatalogCacheEvictor> logger)
{
    public async Task EvictAsync(string tag, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveByTagAsync(tag, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Cache eviction failed for tag {Tag}; entries stay until their TTL expires", tag);
        }
    }
}
