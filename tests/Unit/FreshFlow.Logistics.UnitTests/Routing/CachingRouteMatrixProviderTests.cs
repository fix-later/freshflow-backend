using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Infrastructure.Persistence;
using FreshFlow.Logistics.Infrastructure.Persistence.Entities;
using FreshFlow.Logistics.Infrastructure.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Routing;

[Trait("Category", "Unit")]
public sealed class CachingRouteMatrixProviderTests
{
    private static readonly RoutePoint[] Points =
    [
        new(10.12345678m, 106.12345678m),
        new(11.23456789m, 107.23456789m)
    ];

    [Fact]
    public async Task GetMatrixAsync_AllPairsCached_ReturnsCachedMatrixAsync()
    {
        var inner = Substitute.For<IRouteMatrixProvider>();
        var store = new RecordingStore();
        store.Add("car:10.1234568:106.1234568:11.2345679:107.2345679", 123, 45);
        store.Add("car:11.2345679:107.2345679:10.1234568:106.1234568", 321, 54);
        var sut = Create(inner, store);

        var result = await sut.GetMatrixAsync(Points, ["car"], default);

        result.Provider.Should().Be("GOONG_CACHED");
        result.IsEstimated.Should().BeFalse();
        result.Warnings.Should().BeEmpty();
        result.Profiles["car"].DistanceMeters.Should().BeEquivalentTo(
            new long[][] { [0, 123], [321, 0] });
        result.Profiles["car"].DurationSeconds.Should().BeEquivalentTo(
            new long[][] { [0, 45], [54, 0] });
        await inner.DidNotReceiveWithAnyArgs().GetMatrixAsync(default!, default!, default);
    }

    [Fact]
    public async Task GetMatrixAsync_OnePairMissing_FetchesAndStoresNonDiagonalPairsAsync()
    {
        var inner = Substitute.For<IRouteMatrixProvider>();
        inner.GetMatrixAsync(Points, Arg.Any<IReadOnlyCollection<string>>(), default)
            .Returns(GoongResult());
        var store = new RecordingStore();
        store.Add("car:10.1234568:106.1234568:11.2345679:107.2345679", 123, 45);
        var sut = Create(inner, store);

        var result = await sut.GetMatrixAsync(Points, ["car"], default);

        result.Provider.Should().Be("GOONG");
        await inner.Received(1).GetMatrixAsync(Points, Arg.Any<IReadOnlyCollection<string>>(), default);
        store.Upserted.Should().HaveCount(2);
        store.Upserted.Should().OnlyContain(row => row.PairKey.Contains("car:", StringComparison.Ordinal));
        store.Upserted.Should().NotContain(row =>
            row.FromLatitude == row.ToLatitude && row.FromLongitude == row.ToLongitude);
    }

    [Fact]
    public async Task GetMatrixAsync_Fallback_DoesNotCacheAsync()
    {
        var inner = Substitute.For<IRouteMatrixProvider>();
        inner.GetMatrixAsync(Points, Arg.Any<IReadOnlyCollection<string>>(), default)
            .Returns(FallbackResult());
        var store = new RecordingStore();
        var sut = Create(inner, store);

        var result = await sut.GetMatrixAsync(Points, ["car"], default);

        result.Provider.Should().Be("HAVERSINE_FALLBACK");
        store.UpsertCalls.Should().Be(0);
        store.Upserted.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMatrixAsync_CacheDisabled_IsPurePassthroughAsync()
    {
        var inner = Substitute.For<IRouteMatrixProvider>();
        var expected = FallbackResult();
        inner.GetMatrixAsync(Points, Arg.Any<IReadOnlyCollection<string>>(), default).Returns(expected);
        var store = new RecordingStore();
        var sut = Create(inner, store, enabled: false);

        var result = await sut.GetMatrixAsync(Points, ["car"], default);

        result.Should().BeSameAs(expected);
        store.GetCalls.Should().Be(0);
        store.UpsertCalls.Should().Be(0);
        await inner.Received(1).GetMatrixAsync(Points, Arg.Any<IReadOnlyCollection<string>>(), default);
    }

    [Fact]
    public async Task GetMatrixAsync_ExpiredPairs_AreTreatedAsMissesAsync()
    {
        var inner = Substitute.For<IRouteMatrixProvider>();
        inner.GetMatrixAsync(Points, Arg.Any<IReadOnlyCollection<string>>(), default)
            .Returns(FallbackResult());
        var store = new RecordingStore();
        store.Add(
            "car:10.1234568:106.1234568:11.2345679:107.2345679",
            123,
            45,
            DateTime.UtcNow.AddDays(-31));
        var sut = Create(inner, store);

        var result = await sut.GetMatrixAsync(Points, ["car"], default);

        result.Provider.Should().Be("HAVERSINE_FALLBACK");
        store.GetCalls.Should().Be(1);
        await inner.Received(1).GetMatrixAsync(Points, Arg.Any<IReadOnlyCollection<string>>(), default);
    }

    private static CachingRouteMatrixProvider Create(
        IRouteMatrixProvider inner,
        IRouteMatrixCacheStore store,
        bool enabled = true)
    {
        var settings = Substitute.For<IVehicleCapacityPolicy>();
        settings.MatrixCacheEnabled.Returns(enabled);
        settings.MatrixCacheMaxAgeDays.Returns(30);
        return new CachingRouteMatrixProvider(
            inner,
            store,
            settings,
            NullLogger<CachingRouteMatrixProvider>.Instance);
    }

    private static RouteMatrixResult GoongResult() => new(
        new Dictionary<string, ProfileRouteMatrix>
        {
            ["car"] = new([[0, 123], [321, 0]], [[0, 45], [54, 0]])
        },
        "GOONG",
        false,
        []);

    private static RouteMatrixResult FallbackResult() => new(
        new Dictionary<string, ProfileRouteMatrix>
        {
            ["car"] = new([[0, 100], [100, 0]], [[0, 10], [10, 0]])
        },
        "HAVERSINE_FALLBACK",
        true,
        ["estimated"]);

    private sealed class RecordingStore : IRouteMatrixCacheStore
    {
        private readonly Dictionary<string, ((long DistanceMeters, long DurationSeconds) Value, DateTime CalculatedAt)>
            _entries = new(StringComparer.Ordinal);

        public int GetCalls { get; private set; }
        public int UpsertCalls { get; private set; }
        public List<RouteMatrixCacheEntry> Upserted { get; } = [];

        public Task<IReadOnlyDictionary<string, (long DistanceMeters, long DurationSeconds)>> GetAsync(
            IEnumerable<string> keys,
            DateTime minCalculatedAt,
            CancellationToken ct)
        {
            GetCalls++;
            IReadOnlyDictionary<string, (long DistanceMeters, long DurationSeconds)> result = keys
                .Where(key => _entries.TryGetValue(key, out var entry)
                    && entry.CalculatedAt >= minCalculatedAt)
                .ToDictionary(key => key, key => _entries[key].Value, StringComparer.Ordinal);
            return Task.FromResult(result);
        }

        public Task UpsertAsync(IEnumerable<RouteMatrixCacheEntry> rows, CancellationToken ct)
        {
            UpsertCalls++;
            Upserted.AddRange(rows);
            return Task.CompletedTask;
        }

        public void Add(
            string key,
            long distanceMeters,
            long durationSeconds,
            DateTime? calculatedAt = null) =>
            _entries[key] = ((distanceMeters, durationSeconds), calculatedAt ?? DateTime.UtcNow);
    }
}
