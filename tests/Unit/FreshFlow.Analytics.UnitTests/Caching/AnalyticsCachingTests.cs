using FluentAssertions;
using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Caching;
using FreshFlow.Analytics.Application.Queries.GetDashboardOverview;
using FreshFlow.SharedKernel.Caching;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Analytics.UnitTests.Caching;

[Trait("Category", "Unit")]
public sealed class AnalyticsCachingTests
{
    // 2026-07-16 16:30 UTC = 2026-07-16 23:30 in Vietnam (UTC+7) — same day, late evening.
    private static readonly DateTimeOffset LateOnJul16Vietnam = new(2026, 7, 16, 16, 30, 0, TimeSpan.Zero);

    // 2026-07-16 17:30 UTC = 2026-07-17 00:30 in Vietnam — one hour later, next business day.
    private static readonly DateTimeOffset JustAfterMidnightJul17Vietnam = new(2026, 7, 16, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task rolls_the_dashboard_onto_the_new_business_day_at_midnight_in_Vietnam()
    {
        // Arrange — one container, one cache, a clock that crosses midnight Vietnam time.
        // If the key were built from an unresolved "today", the second read would keep serving
        // 16 July's numbers for the rest of the TTL even though it is already 17 July in Vietnam.
        var clock = new MovableClock(LateOnJul16Vietnam);
        var reader = new RecordingDashboardReader();
        var sender = CreateSender(reader, clock);

        // Act
        await sender.Send(new GetDashboardOverviewQuery());
        clock.Now = JustAfterMidnightJul17Vietnam;
        await sender.Send(new GetDashboardOverviewQuery());

        // Assert
        reader.DatesRead.Should().Equal(new DateOnly(2026, 7, 16), new DateOnly(2026, 7, 17));
    }

    [Fact]
    public async Task serves_the_same_business_day_from_cache_without_touching_the_database_again()
    {
        // Arrange
        var clock = new MovableClock(LateOnJul16Vietnam);
        var reader = new RecordingDashboardReader();
        var sender = CreateSender(reader, clock);

        // Act — same day, ten minutes apart
        await sender.Send(new GetDashboardOverviewQuery());
        clock.Now = LateOnJul16Vietnam.AddMinutes(10);
        await sender.Send(new GetDashboardOverviewQuery());

        // Assert
        reader.DatesRead.Should().Equal(new DateOnly(2026, 7, 16));
    }

    [Fact]
    public void holds_a_settled_past_range_far_longer_than_one_that_still_moves()
    {
        // Arrange
        var cache = CreateAnalyticsCache(new MovableClock(LateOnJul16Vietnam));

        // Act
        var yesterday = cache.OptionsFor(new DateOnly(2026, 7, 15));
        var today = cache.OptionsFor(new DateOnly(2026, 7, 16));
        var tomorrow = cache.OptionsFor(new DateOnly(2026, 7, 17));

        // Assert — only a range that ended before today can no longer change
        yesterday.Expiration.Should().Be(AnalyticsCacheTtl.Closed);
        today.Expiration.Should().Be(AnalyticsCacheTtl.Open);
        tomorrow.Expiration.Should().Be(AnalyticsCacheTtl.Open);
    }

    [Fact]
    public void treats_the_same_product_ids_in_any_order_as_one_cache_entry()
    {
        // Arrange
        var a = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var b = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var from = new DateOnly(2026, 7, 1);
        var to = new DateOnly(2026, 7, 10);

        // Act
        var ab = RedisKeys.AnalyticsPriceTrends([a, b], from, to, "day");
        var ba = RedisKeys.AnalyticsPriceTrends([b, a], from, to, "day");

        // Assert
        ba.Should().Be(ab);
    }

    [Fact]
    public void never_serves_an_unfiltered_query_from_a_filtered_entry()
    {
        // Arrange
        var from = new DateOnly(2026, 7, 1);
        var to = new DateOnly(2026, 7, 10);
        var restaurant = Guid.NewGuid();

        // Act
        var unfiltered = RedisKeys.AnalyticsOrderMetrics(from, to, null, null);
        var filtered = RedisKeys.AnalyticsOrderMetrics(from, to, restaurant, null);
        var grouped = RedisKeys.AnalyticsOrderMetrics(from, to, null, "week");

        // Assert
        unfiltered.Should().NotBe(filtered);
        unfiltered.Should().NotBe(grouped);
    }

    [Fact]
    public void writes_dates_into_keys_the_same_way_on_every_machine()
    {
        // A culture-dependent date format would give two hosts different keys for one query.
        RedisKeys.AnalyticsDemandHeatmap(new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31))
            .Should().Be("analytics:demand-heatmap:2026-07-01:2026-12-31");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static class AnalyticsCacheTtl
    {
        public static readonly TimeSpan Open = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan Closed = TimeSpan.FromHours(6);
    }

    private static AnalyticsCache CreateAnalyticsCache(TimeProvider clock) =>
        new(new ServiceCollection().AddHybridCache().Services
                .BuildServiceProvider().GetRequiredService<HybridCache>(),
            clock);

    private static ISender CreateSender(IDashboardOverviewReader reader, TimeProvider clock)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(reader);
        services.AddSingleton(clock);
        services.AddHybridCache();
        services.AddScoped<AnalyticsCache>();
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(GetDashboardOverviewQuery).Assembly));
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private sealed class MovableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingDashboardReader : IDashboardOverviewReader
    {
        private readonly List<DateOnly> _datesRead = [];

        public IReadOnlyList<DateOnly> DatesRead => _datesRead;

        public Task<DashboardOverviewReadModel> ReadAsync(
            DateOnly date, DateTime startUtcInclusive, DateTime endUtcExclusive, CancellationToken ct)
        {
            _datesRead.Add(date);
            return Task.FromResult(new DashboardOverviewReadModel(0, 0m, 0, 0, 0, 0, 0, 0m, 0m));
        }
    }
}
