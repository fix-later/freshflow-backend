using FluentAssertions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;

namespace FreshFlow.Logistics.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class DeliveryRouteTests
{
    [Fact]
    public void CreateDirect_MissingMarketStop_ThrowsArgumentException()
    {
        var stops = new[] { RestaurantStop() };

        var act = () => DeliveryRoute.CreateDirect(DateOnly.FromDateTime(DateTime.UtcNow), stops, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDirect_MissingRestaurantStop_ThrowsArgumentException()
    {
        var stops = new[] { MarketStop() };

        var act = () => DeliveryRoute.CreateDirect(DateOnly.FromDateTime(DateTime.UtcNow), stops, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDirect_MoreThan20Stops_ThrowsArgumentException()
    {
        var stops = Enumerable.Range(0, 21)
            .Select(index => index == 0
                ? MarketStop(stopOrder: index)
                : RestaurantStop(stopOrder: index))
            .ToList();

        var act = () => DeliveryRoute.CreateDirect(DateOnly.FromDateTime(DateTime.UtcNow), stops, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDirect_ValidStops_CreatesPlannedDirectRoute()
    {
        var serviceDate = new DateOnly(2026, 7, 9);
        var createdBy = Guid.NewGuid();

        var route = DeliveryRoute.CreateDirect(serviceDate, [MarketStop(), RestaurantStop(1)], createdBy);

        route.Id.Should().NotBeEmpty();
        route.RouteType.Should().Be(RouteType.direct);
        route.Status.Should().Be(RouteStatus.planned);
        route.ServiceDate.Should().Be(serviceDate);
        route.CreatedBy.Should().Be(createdBy);
        route.Stops.Should().HaveCount(2);
        route.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        route.UpdatedAt.Should().Be(route.CreatedAt);
    }

    [Fact]
    public void Select_PlannedRoute_TransitionsToSelected()
    {
        var route = DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 9),
            [MarketStop(), RestaurantStop(1)],
            null);

        route.Select();

        route.Status.Should().Be(RouteStatus.selected);
        route.UpdatedAt.Should().BeAfter(route.CreatedAt);
    }

    [Fact]
    public void Select_AlreadySelectedRoute_ThrowsInvalidOperationException()
    {
        var route = DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 9),
            [MarketStop(), RestaurantStop(1)],
            null);
        route.Select();

        var act = route.Select;

        act.Should().Throw<InvalidOperationException>();
    }

    private static RouteStop MarketStop(int stopOrder = 0) =>
        new(
            stopOrder,
            StopEntityType.market,
            Guid.NewGuid(),
            "Market",
            10.1m,
            106.1m,
            null,
            null);

    private static RouteStop RestaurantStop(int stopOrder = 0) =>
        new(
            stopOrder,
            StopEntityType.restaurant,
            Guid.NewGuid(),
            "Restaurant",
            10.2m,
            106.2m,
            null,
            null);
}
