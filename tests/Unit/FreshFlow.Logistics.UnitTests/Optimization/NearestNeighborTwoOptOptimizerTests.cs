using FluentAssertions;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.Infrastructure.Optimization;
using Microsoft.Extensions.Configuration;

namespace FreshFlow.Logistics.UnitTests.Optimization;

[Trait("Category", "Unit")]
public sealed class NearestNeighborTwoOptOptimizerTests
{
    [Fact]
    public void Optimize_ThreeRestaurantStops_ReducesDistanceVersusInputOrder()
    {
        var sut = CreateSut();
        var stops = new[]
        {
            Stop(0, StopEntityType.market, "Market", 0m, 0m),
            Stop(1, StopEntityType.restaurant, "Far East", 0m, 3m),
            Stop(2, StopEntityType.restaurant, "Near North", 2m, 0m),
            Stop(3, StopEntityType.restaurant, "Near East", 0m, 1m)
        };
        var inputDistance = TotalDistance(stops);

        var result = sut.Optimize(stops, new DateOnly(2026, 7, 9), OptimizationCriteria.distance);

        result.TotalDistanceKm.Should().BeLessThanOrEqualTo(Math.Round(inputDistance, 2, MidpointRounding.AwayFromZero));
        result.Stops[0].EntityName.Should().Be("Market");
        result.Stops[1].EntityName.Should().Be("Near East");
        result.Stops.Select(stop => stop.StopOrder).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void Optimize_OneRestaurantStop_KeepsOriginalOrder()
    {
        var sut = CreateSut();
        var marketId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var stops = new[]
        {
            Stop(0, StopEntityType.market, "Market", 0m, 0m, marketId),
            Stop(1, StopEntityType.restaurant, "Restaurant", 0m, 1m, restaurantId)
        };

        var result = sut.Optimize(stops, new DateOnly(2026, 7, 9), OptimizationCriteria.cost);

        result.Stops.Select(stop => stop.EntityId).Should().Equal(marketId, restaurantId);
        result.Stops.Select(stop => stop.StopOrder).Should().Equal(0, 1);
    }

    [Theory]
    [InlineData(OptimizationCriteria.distance)]
    [InlineData(OptimizationCriteria.time)]
    [InlineData(OptimizationCriteria.cost)]
    public void Optimize_AllCriteria_CalculateDistanceDurationAndCost(OptimizationCriteria criteria)
    {
        var sut = CreateSut(avgSpeedKmh: 60, costPerKm: 1000);
        var stops = new[]
        {
            Stop(0, StopEntityType.market, "Market", 0m, 0m),
            Stop(1, StopEntityType.restaurant, "Restaurant", 0m, 1m)
        };

        var result = sut.Optimize(stops, new DateOnly(2026, 7, 9), criteria);

        result.TotalDistanceKm.Should().BeGreaterThan(0);
        result.EstimatedDurationMinutes.Should().BeGreaterThan(0);
        result.EstimatedCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Optimize_AppliesProvisionalEtasFromConfiguredStartHourAndSpeed()
    {
        var serviceDate = new DateOnly(2026, 7, 9);
        var sut = CreateSut(avgSpeedKmh: 60, serviceTimeMinutes: 5, startHour: 7);
        var stops = new[]
        {
            Stop(0, StopEntityType.market, "Market", 0m, 0m),
            Stop(1, StopEntityType.restaurant, "Restaurant", 0m, 1m)
        };

        var result = sut.Optimize(stops, serviceDate, OptimizationCriteria.time);

        var expectedStart = serviceDate.ToDateTime(new TimeOnly(7, 0));
        var expectedSecondArrival = expectedStart
            .AddMinutes(5)
            .AddMinutes(HaversineDistanceCalculator.DistanceKm(0m, 0m, 0m, 1m) / 60 * 60);
        result.Stops[0].EstimatedArrivalAt.Should().Be(expectedStart);
        result.Stops[0].EstimatedDepartureAt.Should().Be(expectedStart.AddMinutes(5));
        result.Stops[1].EstimatedArrivalAt.Should().BeCloseTo(expectedSecondArrival, TimeSpan.FromSeconds(1));
        result.Stops[1].EstimatedDepartureAt.Should().BeCloseTo(expectedSecondArrival.AddMinutes(5), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Optimize_MalformedAvgSpeedKmhConfig_FallsBackToDefault()
    {
        var sut = CreateSutWithConfig(("Logistics:Optimization:AvgSpeedKmh", "not-a-number"));
        var stops = SimpleStops();

        var result = sut.Optimize(stops, new DateOnly(2026, 7, 9), OptimizationCriteria.time);

        result.EstimatedDurationMinutes.Should().Be(ExpectedDurationMinutes(30));
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("0")]
    public void Optimize_NegativeOrZeroAvgSpeedKmhConfig_FallsBackToDefault(string avgSpeedKmh)
    {
        var sut = CreateSutWithConfig(("Logistics:Optimization:AvgSpeedKmh", avgSpeedKmh));
        var stops = SimpleStops();

        var result = sut.Optimize(stops, new DateOnly(2026, 7, 9), OptimizationCriteria.time);

        result.EstimatedDurationMinutes.Should().Be(ExpectedDurationMinutes(30));
    }

    [Fact]
    public void Optimize_NegativeCostPerKmConfig_FallsBackToDefault()
    {
        var sut = CreateSutWithConfig(("Logistics:Optimization:CostPerKm", "-100"));
        var stops = SimpleStops();

        var result = sut.Optimize(stops, new DateOnly(2026, 7, 9), OptimizationCriteria.cost);

        result.EstimatedCost.Should().Be(result.TotalDistanceKm * 5000);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(-1)]
    public void Optimize_StartHourOutOfRange_FallsBackToDefault(int startHour)
    {
        var serviceDate = new DateOnly(2026, 7, 9);
        var sut = CreateSutWithConfig(("Logistics:Optimization:StartHour", startHour.ToString()));
        var stops = SimpleStops();

        var result = sut.Optimize(stops, serviceDate, OptimizationCriteria.time);

        result.Stops[0].EstimatedArrivalAt.Should().Be(serviceDate.ToDateTime(new TimeOnly(6, 0)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    public void Optimize_ValidStartHourBoundary_UsesConfiguredValue(int startHour)
    {
        var serviceDate = new DateOnly(2026, 7, 9);
        var sut = CreateSutWithConfig(("Logistics:Optimization:StartHour", startHour.ToString()));
        var stops = SimpleStops();

        var result = sut.Optimize(stops, serviceDate, OptimizationCriteria.time);

        result.Stops[0].EstimatedArrivalAt.Should().Be(serviceDate.ToDateTime(new TimeOnly(startHour, 0)));
    }

    private static NearestNeighborTwoOptOptimizer CreateSut(
        double avgSpeedKmh = 30,
        decimal costPerKm = 5000,
        int serviceTimeMinutes = 10,
        int startHour = 6)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logistics:Optimization:AvgSpeedKmh"] = avgSpeedKmh.ToString(),
                ["Logistics:Optimization:CostPerKm"] = costPerKm.ToString(),
                ["Logistics:Optimization:ServiceTimeMinutes"] = serviceTimeMinutes.ToString(),
                ["Logistics:Optimization:StartHour"] = startHour.ToString()
            })
            .Build();

        return new NearestNeighborTwoOptOptimizer(config);
    }

    private static NearestNeighborTwoOptOptimizer CreateSutWithConfig(params (string Key, string? Value)[] values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(value => value.Key, value => value.Value))
            .Build();

        return new NearestNeighborTwoOptOptimizer(config);
    }

    private static RouteStop[] SimpleStops() =>
    [
        Stop(0, StopEntityType.market, "Market", 0m, 0m),
        Stop(1, StopEntityType.restaurant, "Restaurant", 0m, 1m)
    ];

    private static RouteStop Stop(
        int stopOrder,
        StopEntityType entityType,
        string name,
        decimal latitude,
        decimal longitude,
        Guid? id = null) =>
        new(
            stopOrder,
            entityType,
            id ?? Guid.NewGuid(),
            name,
            latitude,
            longitude,
            null,
            null);

    private static decimal TotalDistance(IReadOnlyList<RouteStop> stops)
    {
        var total = 0d;
        for (var i = 1; i < stops.Count; i++)
        {
            total += HaversineDistanceCalculator.DistanceKm(
                stops[i - 1].Latitude,
                stops[i - 1].Longitude,
                stops[i].Latitude,
                stops[i].Longitude);
        }

        return (decimal)total;
    }

    private static int ExpectedDurationMinutes(double avgSpeedKmh) =>
        (int)Math.Round(
            HaversineDistanceCalculator.DistanceKm(0m, 0m, 0m, 1m) / avgSpeedKmh * 60,
            MidpointRounding.AwayFromZero);
}
