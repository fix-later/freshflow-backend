using System.Globalization;
using FreshFlow.Pricing.Application.Abstractions;
using StackExchange.Redis;

namespace FreshFlow.Pricing.Infrastructure.Cache;

/// <summary>
/// Redis implementation of <see cref="IPriceBoardCacheWriter"/>.
///
/// Key pattern : <c>price:{marketId}:{productId}</c> (Hash)
/// Fields written: <c>price</c>, <c>quantity</c>, <c>updated_at</c>, <c>updated_by</c>
/// TTL          : 5-minute absolute window, reset on every write (HSET then KeyExpire).
///
/// The <c>reserved</c> field is intentionally excluded — it is owned by the Orders
/// module and must not be overwritten here. HSET only sets the listed fields; any
/// existing fields (e.g. <c>reserved</c>) remain intact.
///
/// Two commands are issued sequentially (HSET → KeyExpire).
/// True atomicity (Lua) is not required: a missed EXPIRE only allows a stale entry
/// to live longer, which is acceptable. Redis failures propagate to callers.
/// </summary>
internal sealed class RedisPriceBoardCache(IConnectionMultiplexer redis) : IPriceBoardCacheWriter
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private const string PriceField = "price";
    private const string QuantityField = "quantity";
    private const string UpdatedAtField = "updated_at";
    private const string UpdatedByField = "updated_by";

    public async Task WriteAsync(
        Guid marketId,
        Guid productId,
        decimal price,
        int quantity,
        DateTime updatedAt,
        Guid? updatedBy,
        CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        RedisKey key = BuildKey(marketId, productId);

        HashEntry[] fields =
        [
            new(PriceField,     price.ToString("F2", CultureInfo.InvariantCulture)),
            new(QuantityField,  quantity.ToString(CultureInfo.InvariantCulture)),
            new(UpdatedAtField, updatedAt.ToString("O", CultureInfo.InvariantCulture)),
            new(UpdatedByField, updatedBy?.ToString() ?? string.Empty),
        ];

        await db.HashSetAsync(key, fields);
        await db.KeyExpireAsync(key, Ttl);
    }

    internal static string BuildKey(Guid marketId, Guid productId) =>
        $"price:{marketId}:{productId}";
}
