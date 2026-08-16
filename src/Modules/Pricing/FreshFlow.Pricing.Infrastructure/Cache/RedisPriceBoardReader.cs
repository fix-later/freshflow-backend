using System.Globalization;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using StackExchange.Redis;

namespace FreshFlow.Pricing.Infrastructure.Cache;

internal sealed class RedisPriceBoardReader(IConnectionMultiplexer redis) : IPriceBoardReader
{
    private static readonly RedisValue[] Fields = ["price", "quantity"];

    public async Task<IReadOnlyDictionary<Guid, LivePriceEntry>> GetBatchAsync(
        Guid marketId,
        IReadOnlyList<Guid> productIds,
        CancellationToken ct = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, LivePriceEntry>();

        ct.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        var reads = productIds.Select(async productId =>
            (productId, values: await db.HashGetAsync(
                RedisPriceBoardCache.BuildKey(marketId, productId), Fields)));
        var rows = await Task.WhenAll(reads);
        ct.ThrowIfCancellationRequested();

        var result = new Dictionary<Guid, LivePriceEntry>();
        foreach (var (productId, values) in rows)
            if (Parse(values) is { } entry)
                result[productId] = entry;

        return result;
    }

    private static LivePriceEntry? Parse(RedisValue[] values)
    {
        if (values.Length != 2
            || !decimal.TryParse(values[0].ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            || !int.TryParse(values[1].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
            || price < 0
            || quantity < 0)
            return null;

        return new LivePriceEntry(price, quantity, quantity);
    }
}
