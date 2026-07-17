using FluentAssertions;
using FluentValidation;
using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Queries.GetDemandHeatmap;
using FreshFlow.Analytics.Application.Queries.GetDemandTimeDistribution;
using FreshFlow.Analytics.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Analytics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetDemandHeatmapQueryHandlerTests
{
    private static readonly DateOnly From = new(2026, 7, 16);
    private static readonly DateOnly To = new(2026, 7, 17);

    [Fact]
    public async Task Handle_AggregatesOrdersRevenueCoordinatesAndCategoryAsync()
    {
        var firstRestaurantId = Guid.NewGuid();
        var secondRestaurantId = Guid.NewGuid();
        var reader = new StubDemandHeatmapReader(
        [
            new(firstRestaurantId, "A Restaurant", 10.75m, 106.67m, "Confirmed", 2, 200m, "Vegetables"),
            new(firstRestaurantId, "A Restaurant", 10.75m, 106.67m, "Cancelled", 1, 500m, "Vegetables"),
            new(firstRestaurantId, "A Restaurant", 10.75m, 106.67m, "Draft", 1, 700m, "Vegetables"),
            new(firstRestaurantId, "A Restaurant", 10.75m, 106.67m, "Delivered", 1, 300m, "Vegetables"),
            new(secondRestaurantId, "B Restaurant", 10.76m, 106.68m, "Confirmed", 1, 50m, null)
        ],
        []);
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetDemandHeatmapQuery(From, To));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Should().BeEquivalentTo(new
        {
            RestaurantId = firstRestaurantId,
            RestaurantName = "A Restaurant",
            Latitude = 10.75m,
            Longitude = 106.67m,
            TotalOrderCount = 5,
            TotalOrderValueVND = 500m,
            DominantProductCategory = "Vegetables"
        });
        result.Value[1].Should().BeEquivalentTo(new
        {
            RestaurantId = secondRestaurantId,
            TotalOrderCount = 1,
            TotalOrderValueVND = 50m,
            DominantProductCategory = (string?)null
        });
        reader.HeatmapStartUtc.Should()
            .Be(new DateTime(2026, 7, 15, 17, 0, 0, DateTimeKind.Utc));
        reader.HeatmapEndUtc.Should()
            .Be(new DateTime(2026, 7, 17, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_RestaurantWithZeroOrders_IsExcludedAsync()
    {
        var excludedRestaurantId = Guid.NewGuid();
        var sender = CreateSender(new StubDemandHeatmapReader([], []));

        var result = await sender.Send(new GetDemandHeatmapQuery(From, From));

        result.Value.Should().NotContain(point => point.RestaurantId == excludedRestaurantId);
    }

    [Fact]
    public async Task Handle_RestaurantWithoutDefaultAddress_IsExcludedWithoutCrashAsync()
    {
        var sender = CreateSender(new StubDemandHeatmapReader([], []));

        var result = await sender.Send(new GetDemandHeatmapQuery(From, From));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UncategorizedProduct_DoesNotRemoveOrderAsync()
    {
        var restaurantId = Guid.NewGuid();
        var sender = CreateSender(new StubDemandHeatmapReader(
        [
            new(restaurantId, "Uncategorized", 10m, 106m, "Confirmed", 1, 25m, null)
        ],
        []));

        var result = await sender.Send(new GetDemandHeatmapQuery(From, From));

        result.Value.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            TotalOrderCount = 1,
            TotalOrderValueVND = 25m,
            DominantProductCategory = (string?)null
        });
    }

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Validation_InvalidRange_ThrowsValidationErrorAsync(GetDemandHeatmapQuery query)
    {
        var sender = CreateSender(new StubDemandHeatmapReader([], []));

        var act = () => sender.Send(query);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().OnlyContain(error => error.ErrorCode == "VALIDATION_ERROR");
    }

    public static TheoryData<GetDemandHeatmapQuery> InvalidQueries => new()
    {
        new GetDemandHeatmapQuery(new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 1)),
        new GetDemandHeatmapQuery(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 3))
    };

    private static ISender CreateSender(IDemandHeatmapReader reader)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnalyticsModule(new ConfigurationBuilder().Build());
        services.AddSingleton(reader);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }
}

[Trait("Category", "Unit")]
public sealed class GetDemandTimeDistributionQueryHandlerTests
{
    private static readonly DateOnly From = new(2026, 7, 16);

    [Fact]
    public async Task Handle_ReturnsAtMost168Cells_WithSundayZeroConventionAndVietnamBoundsAsync()
    {
        var cells = Enumerable.Range(0, 7)
            .SelectMany(day => Enumerable.Range(0, 24)
                .Select(hour => new TimeDistributionCellReadModel(day, hour, 1)))
            .ToArray();
        var reader = new StubDemandHeatmapReader([], cells);
        var sender = CreateSender(reader);

        var result = await sender.Send(new GetDemandTimeDistributionQuery(From, From));

        ((int)DayOfWeek.Sunday).Should().Be(0);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(168);
        result.Value.Should().OnlyContain(cell =>
            cell.DayOfWeek >= 0 && cell.DayOfWeek <= 6 &&
            cell.HourOfDay >= 0 && cell.HourOfDay <= 23);
        result.Value.Should().ContainSingle(cell => cell.DayOfWeek == 0 && cell.HourOfDay == 23);
        reader.TimeStartUtc.Should()
            .Be(new DateTime(2026, 7, 15, 17, 0, 0, DateTimeKind.Utc));
        reader.TimeEndUtc.Should()
            .Be(new DateTime(2026, 7, 16, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_NoOrders_ReturnsEmptyListAsync()
    {
        var sender = CreateSender(new StubDemandHeatmapReader([], []));

        var result = await sender.Send(new GetDemandTimeDistributionQuery(From, From));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Validation_InvalidRange_ThrowsValidationErrorAsync(
        GetDemandTimeDistributionQuery query)
    {
        var sender = CreateSender(new StubDemandHeatmapReader([], []));

        var act = () => sender.Send(query);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().OnlyContain(error => error.ErrorCode == "VALIDATION_ERROR");
    }

    public static TheoryData<GetDemandTimeDistributionQuery> InvalidQueries => new()
    {
        new GetDemandTimeDistributionQuery(new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 1)),
        new GetDemandTimeDistributionQuery(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 3))
    };

    private static ISender CreateSender(IDemandHeatmapReader reader)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnalyticsModule(new ConfigurationBuilder().Build());
        services.AddSingleton(reader);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }
}

internal sealed class StubDemandHeatmapReader(
    IReadOnlyList<DemandHeatmapAggregateReadModel> heatmap,
    IReadOnlyList<TimeDistributionCellReadModel> timeDistribution) : IDemandHeatmapReader
{
    public DateTime HeatmapStartUtc { get; private set; }
    public DateTime HeatmapEndUtc { get; private set; }
    public DateTime TimeStartUtc { get; private set; }
    public DateTime TimeEndUtc { get; private set; }

    public Task<IReadOnlyList<DemandHeatmapAggregateReadModel>> ReadHeatmapAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct)
    {
        HeatmapStartUtc = startUtcInclusive;
        HeatmapEndUtc = endUtcExclusive;
        return Task.FromResult(heatmap);
    }

    public Task<IReadOnlyList<TimeDistributionCellReadModel>> ReadTimeDistributionAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        CancellationToken ct)
    {
        TimeStartUtc = startUtcInclusive;
        TimeEndUtc = endUtcExclusive;
        return Task.FromResult(timeDistribution);
    }
}
