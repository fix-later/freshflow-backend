using FluentAssertions;
using FreshFlow.Pricing.Infrastructure.Cache;
using NSubstitute;
using StackExchange.Redis;

namespace FreshFlow.Pricing.UnitTests.Cache;

[Trait("Category", "Unit")]
public sealed class RedisPriceBoardReaderTests
{
    [Fact]
    public async Task GetBatchAsync_ReturnsValidHitsAndSkipsMissesAsync()
    {
        var marketId = Guid.NewGuid();
        var hitId = Guid.NewGuid();
        var missId = Guid.NewGuid();
        var db = Substitute.For<IDatabase>();
        db.HashGetAsync(
                (RedisKey)RedisPriceBoardCache.BuildKey(marketId, hitId),
                Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>())
            .Returns(["125000.50", "42"]);
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);

        var result = await new RedisPriceBoardReader(redis)
            .GetBatchAsync(marketId, [hitId, missId]);

        result.Should().ContainSingle();
        result[hitId].Price.Should().Be(125000.50m);
        result[hitId].Quantity.Should().Be(42);
        result[hitId].AvailableQuantity.Should().Be(42);
    }
}
