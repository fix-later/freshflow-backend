using System.Globalization;
using FluentAssertions;
using FreshFlow.Pricing.Infrastructure.Cache;
using NSubstitute;
using NSubstitute.Core;
using StackExchange.Redis;

namespace FreshFlow.Pricing.UnitTests.Cache;

/// <summary>
/// Unit tests for <see cref="RedisPriceBoardCache"/>.
///
/// Uses NSubstitute mocks for <see cref="IConnectionMultiplexer"/> and <see cref="IDatabase"/>
/// to verify HSET field values, key pattern, field count (no reserved), and TTL
/// without a live Redis instance.
///
/// Note on KeyExpireAsync assertion strategy: StackExchange.Redis 2.8 has four overloads
/// of <c>KeyExpireAsync</c> differing by <c>TimeSpan?</c> vs <c>DateTime?</c>.
/// NSubstitute's <c>Arg</c> matchers can be ambiguous across them, so TTL and ordering
/// tests use <c>db.ReceivedCalls()</c> direct inspection instead.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RedisPriceBoardCacheTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private static readonly Guid MarketId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid UserId = Guid.Parse("33333333-0000-0000-0000-000000000003");

    private static (RedisPriceBoardCache cache, IDatabase db) BuildSut()
    {
        var db = Substitute.For<IDatabase>();
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        return (new RedisPriceBoardCache(multiplexer), db);
    }

    private Task CallWriteAsync(
        RedisPriceBoardCache cache,
        Guid? marketId = null,
        Guid? productId = null,
        decimal price = 125000m,
        int quantity = 50,
        DateTime? updatedAt = null,
        Guid? updatedBy = null) =>
        cache.WriteAsync(
            marketId ?? MarketId,
            productId ?? ProductId,
            price,
            quantity,
            updatedAt ?? new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc),
            updatedBy);

    // ── Key pattern ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildKey_ReturnsExpectedPattern()
    {
        var key = RedisPriceBoardCache.BuildKey(MarketId, ProductId);
        key.Should().Be($"price:{MarketId}:{ProductId}");
    }

    // ── HSET calls ────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_CallsHashSetWithCorrectKey()
    {
        // Arrange
        var (cache, db) = BuildSut();

        // Act
        await CallWriteAsync(cache);

        // Assert
        await db.Received(1).HashSetAsync(
            Arg.Is<RedisKey>(k => (string)k == $"price:{MarketId}:{ProductId}"),
            Arg.Any<HashEntry[]>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task WriteAsync_HashContainsFourFieldsNoReserved()
    {
        // Arrange
        var (cache, db) = BuildSut();
        HashEntry[]? capturedFields = null;
        await db.HashSetAsync(Arg.Any<RedisKey>(),
            Arg.Do<HashEntry[]>(f => capturedFields = f),
            Arg.Any<CommandFlags>());

        // Act
        await CallWriteAsync(cache);

        // Assert — exactly 4 fields: price, quantity, updated_at, updated_by (NO reserved)
        capturedFields.Should().NotBeNull();
        capturedFields!.Should().HaveCount(4);
        capturedFields.Select(f => f.Name.ToString()).Should()
            .BeEquivalentTo(["price", "quantity", "updated_at", "updated_by"]);
        capturedFields.Select(f => f.Name.ToString()).Should()
            .NotContain("reserved", "reserved is owned by the Orders module");
    }

    [Fact]
    public async Task WriteAsync_PriceFormattedAsF2InvariantCulture()
    {
        // Arrange
        var (cache, db) = BuildSut();
        HashEntry[]? capturedFields = null;
        await db.HashSetAsync(Arg.Any<RedisKey>(),
            Arg.Do<HashEntry[]>(f => capturedFields = f),
            Arg.Any<CommandFlags>());

        // Act
        await CallWriteAsync(cache, price: 125000.5m);

        // Assert
        capturedFields.Should().NotBeNull();
        capturedFields!.First(f => f.Name == "price").Value.ToString()
            .Should().Be("125000.50");
    }

    [Fact]
    public async Task WriteAsync_QuantityStoredAsIntString()
    {
        // Arrange
        var (cache, db) = BuildSut();
        HashEntry[]? capturedFields = null;
        await db.HashSetAsync(Arg.Any<RedisKey>(),
            Arg.Do<HashEntry[]>(f => capturedFields = f),
            Arg.Any<CommandFlags>());

        // Act
        await CallWriteAsync(cache, quantity: 42);

        // Assert
        capturedFields.Should().NotBeNull();
        capturedFields!.First(f => f.Name == "quantity").Value.ToString()
            .Should().Be("42");
    }

    [Fact]
    public async Task WriteAsync_UpdatedAtStoredAsIso8601()
    {
        // Arrange
        var (cache, db) = BuildSut();
        var at = new DateTime(2026, 6, 13, 10, 30, 0, DateTimeKind.Utc);
        HashEntry[]? capturedFields = null;
        await db.HashSetAsync(Arg.Any<RedisKey>(),
            Arg.Do<HashEntry[]>(f => capturedFields = f),
            Arg.Any<CommandFlags>());

        // Act
        await CallWriteAsync(cache, updatedAt: at);

        // Assert — "O" round-trip specifier
        capturedFields.Should().NotBeNull();
        capturedFields!.First(f => f.Name == "updated_at").Value.ToString()
            .Should().Be(at.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task WriteAsync_UpdatedByStoredAsGuidString()
    {
        // Arrange
        var (cache, db) = BuildSut();
        HashEntry[]? capturedFields = null;
        await db.HashSetAsync(Arg.Any<RedisKey>(),
            Arg.Do<HashEntry[]>(f => capturedFields = f),
            Arg.Any<CommandFlags>());

        // Act
        await CallWriteAsync(cache, updatedBy: UserId);

        // Assert
        capturedFields.Should().NotBeNull();
        capturedFields!.First(f => f.Name == "updated_by").Value.ToString()
            .Should().Be(UserId.ToString());
    }

    [Fact]
    public async Task WriteAsync_NullUpdatedByStoredAsEmptyString()
    {
        // Arrange
        var (cache, db) = BuildSut();
        HashEntry[]? capturedFields = null;
        await db.HashSetAsync(Arg.Any<RedisKey>(),
            Arg.Do<HashEntry[]>(f => capturedFields = f),
            Arg.Any<CommandFlags>());

        // Act
        await CallWriteAsync(cache, updatedBy: null);

        // Assert
        capturedFields.Should().NotBeNull();
        capturedFields!.First(f => f.Name == "updated_by").Value.ToString()
            .Should().BeEmpty();
    }

    // ── TTL ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_SetsKeyExpireToFiveMinutes()
    {
        // Arrange
        var (cache, db) = BuildSut();

        // Act
        await CallWriteAsync(cache);

        // Assert — inspect ReceivedCalls() directly (avoids NSubstitute TimeSpan?/DateTime? ambiguity).
        IReadOnlyCollection<ICall> allCalls = db.ReceivedCalls().ToList();
        var expireCalls = allCalls
            .Where(c => c.GetMethodInfo().Name == "KeyExpireAsync")
            .ToList();

        expireCalls.Should().HaveCount(1, "KeyExpire should be called once after HashSet");
        var args = expireCalls[0].GetArguments();
        ((string)(RedisKey)args[0]!).Should().Be($"price:{MarketId}:{ProductId}");
        args[1].Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task WriteAsync_HashSetCalledBeforeKeyExpire()
    {
        // Arrange
        var (cache, db) = BuildSut();

        // Act
        await CallWriteAsync(cache);

        // Assert — ordering via ReceivedCalls()
        var callNames = db.ReceivedCalls()
            .Select(c => c.GetMethodInfo().Name)
            .ToList();

        callNames.Should().Contain("HashSetAsync");
        callNames.Should().Contain("KeyExpireAsync");
        callNames.IndexOf("HashSetAsync").Should().BeLessThan(
            callNames.IndexOf("KeyExpireAsync"),
            "HSET must be committed before TTL is reset");
    }
}
