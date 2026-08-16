using FluentAssertions;
using FluentValidation;
using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Analytics.Application.Queries.GetOrderMetrics;
using FreshFlow.Analytics.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Analytics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetOrderMetricsQueryHandlerTests
{
    private static readonly Guid RestaurantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_NormalSet_ReturnsCorrectMetricsBucketsStatusesAndVietnamBoundsAsync()
    {
        var reader = new StubOrderMetricsReader(
        [
            new(new DateOnly(2026, 7, 17), "Draft", 1, 700m),
            new(new DateOnly(2026, 7, 16), "Cancelled", 2, 1_000m),
            new(new DateOnly(2026, 7, 17), "Delivered", 2, 400m),
            new(new DateOnly(2026, 7, 16), "Confirmed", 1, 100m)
        ]);
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetOrderMetricsQuery(
            new DateOnly(2026, 7, 16),
            new DateOnly(2026, 7, 17),
            RestaurantId,
            null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.TotalOrders.Should().Be(6);
        result.Value.Summary.TotalRevenueVND.Should().Be(500m);
        result.Value.Summary.AvgOrderValueVND.Should().Be(500m / 6m);
        result.Value.Summary.CancelledCount.Should().Be(2);
        result.Value.Summary.CancellationRatePercent.Should().Be(33.33m);
        result.Value.Summary.DeliveredCount.Should().Be(2);
        result.Value.Summary.StatusCounts.Should().HaveCount(8);
        result.Value.Summary.StatusCounts.Should().Contain(new Dictionary<string, int>
        {
            ["Draft"] = 1,
            ["Confirmed"] = 1,
            ["Batched"] = 0,
            ["PickedUp"] = 0,
            ["AtHub"] = 0,
            ["Delivering"] = 0,
            ["Delivered"] = 2,
            ["Cancelled"] = 2
        });
        result.Value.Buckets.Should().Equal(
            new OrderMetricsBucketDto(new DateOnly(2026, 7, 16), 3, 100m),
            new OrderMetricsBucketDto(new DateOnly(2026, 7, 17), 3, 400m));
        reader.StartUtc.Should().Be(new DateTime(2026, 7, 15, 17, 0, 0, DateTimeKind.Utc));
        reader.EndUtc.Should().Be(new DateTime(2026, 7, 17, 17, 0, 0, DateTimeKind.Utc));
        reader.RestaurantId.Should().Be(RestaurantId);
        reader.GroupBy.Should().Be("day");
    }

    [Fact]
    public async Task Handle_NoOrders_ReturnsZerosAndAllStatusesAsync()
    {
        var sender = CreateSender(new StubOrderMetricsReader([]));

        var result = await sender.Send(new GetOrderMetricsQuery(
            new DateOnly(2026, 7, 16),
            new DateOnly(2026, 7, 16),
            null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.TotalOrders.Should().Be(0);
        result.Value.Summary.TotalRevenueVND.Should().Be(0m);
        result.Value.Summary.AvgOrderValueVND.Should().Be(0m);
        result.Value.Summary.CancelledCount.Should().Be(0);
        result.Value.Summary.CancellationRatePercent.Should().Be(0m);
        result.Value.Summary.DeliveredCount.Should().Be(0);
        result.Value.Summary.StatusCounts.Should().HaveCount(8);
        result.Value.Summary.StatusCounts.Values.Should().OnlyContain(count => count == 0);
        result.Value.Buckets.Should().BeEmpty();
    }

    [Theory]
    [InlineData("WEEK", "week", 2026, 7, 13)]
    [InlineData("Month", "month", 2026, 7, 1)]
    public async Task Handle_GroupBy_NormalizesAndReturnsOrderedReaderBucketsAsync(
        string requestedGroupBy,
        string expectedGroupBy,
        int year,
        int month,
        int day)
    {
        var bucketDate = new DateOnly(year, month, day);
        var reader = new StubOrderMetricsReader(
        [
            new(bucketDate, "Confirmed", 1, 100m)
        ]);
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetOrderMetricsQuery(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            null,
            requestedGroupBy));

        reader.GroupBy.Should().Be(expectedGroupBy);
        result.Value.Buckets.Should().ContainSingle()
            .Which.Date.Should().Be(bucketDate);
    }

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Validation_InvalidRequest_ThrowsValidationErrorAsync(GetOrderMetricsQuery query)
    {
        var sender = CreateSender(new StubOrderMetricsReader([]));

        var act = () => sender.Send(query);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().OnlyContain(error => error.ErrorCode == "VALIDATION_ERROR");
    }

    public static TheoryData<GetOrderMetricsQuery> InvalidQueries => new()
    {
        new GetOrderMetricsQuery(
            new DateOnly(2026, 7, 2),
            new DateOnly(2026, 7, 1),
            null),
        new GetOrderMetricsQuery(
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 1, 3),
            null),
        new GetOrderMetricsQuery(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 2),
            null,
            "quarter"),
        new GetOrderMetricsQuery(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 2),
            null,
            "'; DROP TABLE orders;--")
    };

    private static ISender CreateSender(IOrderMetricsReader reader)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnalyticsModule(new ConfigurationBuilder().Build());
        services.AddSingleton(reader);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    private sealed class StubOrderMetricsReader(
        IReadOnlyList<OrderMetricsBucketReadModel> result) : IOrderMetricsReader
    {
        public DateTime StartUtc { get; private set; }
        public DateTime EndUtc { get; private set; }
        public Guid? RestaurantId { get; private set; }
        public string? GroupBy { get; private set; }

        public Task<IReadOnlyList<OrderMetricsBucketReadModel>> ReadAsync(
            DateTime startUtcInclusive,
            DateTime endUtcExclusive,
            Guid? restaurantId,
            string groupBy,
            CancellationToken ct)
        {
            StartUtc = startUtcInclusive;
            EndUtc = endUtcExclusive;
            RestaurantId = restaurantId;
            GroupBy = groupBy;
            return Task.FromResult(result);
        }
    }
}
