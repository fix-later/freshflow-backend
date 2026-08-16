using FluentAssertions;
using FluentValidation;
using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Queries.GetPriceTrends;
using FreshFlow.Analytics.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Analytics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetPriceTrendsQueryHandlerTests
{
    private static readonly Guid MarketProductId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Hourly_ReturnsOrderedPointsSummaryAndVietnamBoundsAsync()
    {
        var reader = new StubPriceTrendReader(new PriceTrendReadModel(
            [
                Bucket(new DateTime(2026, 7, 16, 16, 0, 0, DateTimeKind.Utc), 30m),
                Bucket(new DateTime(2026, 7, 15, 17, 0, 0, DateTimeKind.Utc), 10m),
                Bucket(new DateTime(2026, 7, 16, 3, 0, 0, DateTimeKind.Utc), 20m)
            ],
            [new MarketProductDetailReadModel(MarketProductId, "Cá lóc", "Chợ Hóc Môn")]));
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetPriceTrendsQuery(
            [MarketProductId],
            new DateOnly(2026, 7, 16),
            new DateOnly(2026, 7, 16),
            "hourly"));

        result.IsSuccess.Should().BeTrue();
        var series = result.Value.Series.Should().ContainSingle().Subject;
        series.ProductName.Should().Be("Cá lóc");
        series.MarketName.Should().Be("Chợ Hóc Môn");
        series.Interval.Should().Be("hourly");
        series.Summary.MinPrice.Should().Be(10m);
        series.Summary.MaxPrice.Should().Be(30m);
        series.Summary.AvgPrice.Should().Be(20m);
        series.Summary.PriceVolatility.Should().BeApproximately(10m, 0.000001m);
        series.Points.Select(point => point.Timestamp.Hour).Should().Equal(0, 10, 23);
        series.Points.Should().OnlyContain(point => point.Timestamp.Offset == TimeSpan.FromHours(7));
        reader.StartUtc.Should().Be(new DateTime(2026, 7, 15, 17, 0, 0, DateTimeKind.Utc));
        reader.EndUtc.Should().Be(new DateTime(2026, 7, 16, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_OneSnapshot_ReturnsNullVolatilityAsync()
    {
        var reader = new StubPriceTrendReader(new PriceTrendReadModel(
            [Bucket(new DateTime(2026, 7, 16, 3, 0, 0, DateTimeKind.Utc), 25m)],
            [new MarketProductDetailReadModel(MarketProductId, "Cá lóc", "Chợ Hóc Môn")]));
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetPriceTrendsQuery(
            [MarketProductId],
            new DateOnly(2026, 7, 16),
            new DateOnly(2026, 7, 16),
            "daily"));

        result.Value.Series.Single().Summary.PriceVolatility.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NoSnapshots_ReturnsEmptySeriesAsync()
    {
        var reader = new StubPriceTrendReader(new PriceTrendReadModel(
            [],
            [new MarketProductDetailReadModel(MarketProductId, "Cá lóc", "Chợ Hóc Môn")]));
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetPriceTrendsQuery(
            [MarketProductId],
            new DateOnly(2026, 7, 16),
            new DateOnly(2026, 7, 16)));

        result.IsSuccess.Should().BeTrue();
        result.Value.Series.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MaxSupportedDate_DoesNotOverflowAsync()
    {
        var reader = new StubPriceTrendReader(new PriceTrendReadModel([], []));
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetPriceTrendsQuery(
            [MarketProductId],
            new DateOnly(9999, 1, 1),
            DateOnly.MaxValue,
            "hourly"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Series.Should().BeEmpty();
        reader.EndUtc.Should().BeAfter(reader.StartUtc);
    }

    [Fact]
    public async Task Handle_RangeOverTwelveMonths_ForcesDailyAsync()
    {
        var reader = new StubPriceTrendReader(new PriceTrendReadModel(
            [
                Bucket(new DateTime(2025, 6, 1, 1, 0, 0, DateTimeKind.Utc), 10m),
                Bucket(new DateTime(2025, 6, 1, 2, 0, 0, DateTimeKind.Utc), 20m)
            ],
            [new MarketProductDetailReadModel(MarketProductId, "Cá lóc", "Chợ Hóc Môn")]));
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetPriceTrendsQuery(
            [MarketProductId],
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 1, 2),
            "hourly"));

        var series = result.Value.Series.Single();
        series.Interval.Should().Be("daily");
        series.Points.Should().ContainSingle();
        series.Points.Single().AvgPrice.Should().Be(15m);
    }

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Validation_InvalidRequest_ThrowsValidationExceptionAsync(
        GetPriceTrendsQuery query)
    {
        var sender = CreateSender(new StubPriceTrendReader(new PriceTrendReadModel([], [])));

        var act = () => sender.Send(query);

        await act.Should().ThrowAsync<ValidationException>();
    }

    public static TheoryData<GetPriceTrendsQuery> InvalidQueries => new()
    {
        new GetPriceTrendsQuery(
            Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 2)),
        new GetPriceTrendsQuery(
            [MarketProductId],
            new DateOnly(2026, 1, 2),
            new DateOnly(2026, 1, 1)),
        new GetPriceTrendsQuery(
            [MarketProductId],
            new DateOnly(2024, 1, 1),
            new DateOnly(2026, 1, 2)),
        new GetPriceTrendsQuery(
            [MarketProductId],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 2),
            "'; DROP TABLE price_snapshots;--")
    };

    private static PriceTrendHourlyBucketReadModel Bucket(DateTime utcHour, decimal price) =>
        new(MarketProductId, utcHour, price, price, price, 1, null);

    private static ISender CreateSender(IPriceTrendReader reader)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnalyticsModule(new ConfigurationBuilder().Build());
        services.AddSingleton(reader);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private sealed class StubPriceTrendReader(PriceTrendReadModel result) : IPriceTrendReader
    {
        public DateTime StartUtc { get; private set; }
        public DateTime EndUtc { get; private set; }

        public Task<PriceTrendReadModel> ReadAsync(
            IReadOnlyList<Guid> marketProductIds,
            DateTime startUtcInclusive,
            DateTime endUtcExclusive,
            CancellationToken ct)
        {
            StartUtc = startUtcInclusive;
            EndUtc = endUtcExclusive;
            return Task.FromResult(result);
        }
    }
}
