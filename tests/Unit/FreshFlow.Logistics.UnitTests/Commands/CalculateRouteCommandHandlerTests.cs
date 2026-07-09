using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Commands.CalculateRoute;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CalculateRouteCommandHandlerTests
{
    [Fact]
    public async Task Handle_HappyPath_PersistsDirectRouteWithInputOrderAsync()
    {
        var marketId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var repository = new InMemoryDeliveryRouteRepository();
        var markets = Substitute.For<IMarketCoordinateReader>();
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        markets.FindByIdAsync(marketId, Arg.Any<CancellationToken>())
            .Returns(new MarketCoordinateDto(marketId, "Market A", 10.1m, 106.1m));
        restaurants.FindByRestaurantIdAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantCoordinateDto(restaurantId, 10.2m, 106.2m));
        var sut = new CalculateRouteCommandHandler(markets, restaurants, repository);

        var result = await sut.Handle(
            new CalculateRouteCommand(
                [marketId],
                [],
                [restaurantId],
                null,
                new DateOnly(2026, 7, 9),
                false),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RouteType.Should().Be("direct");
        result.Value.Status.Should().Be("planned");
        result.Value.Stops.Should().HaveCount(2);
        result.Value.Stops[0].EntityType.Should().Be("market");
        result.Value.Stops[0].EntityId.Should().Be(marketId);
        result.Value.Stops[0].EntityName.Should().Be("Market A");
        result.Value.Stops[1].EntityType.Should().Be("restaurant");
        result.Value.Stops[1].EntityId.Should().Be(restaurantId);
        repository.Routes.Should().ContainSingle(route => route.RouteType == RouteType.direct);
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MarketMissing_ReturnsNotFoundAsync()
    {
        var marketId = Guid.NewGuid();
        var repository = new InMemoryDeliveryRouteRepository();
        var markets = Substitute.For<IMarketCoordinateReader>();
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        var sut = new CalculateRouteCommandHandler(markets, restaurants, repository);

        var result = await sut.Handle(Command(marketId, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_NOT_FOUND");
        repository.Routes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RestaurantMissing_ReturnsNotFoundAsync()
    {
        var marketId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var repository = new InMemoryDeliveryRouteRepository();
        var markets = Substitute.For<IMarketCoordinateReader>();
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        markets.FindByIdAsync(marketId, Arg.Any<CancellationToken>())
            .Returns(new MarketCoordinateDto(marketId, "Market A", 10.1m, 106.1m));
        var sut = new CalculateRouteCommandHandler(markets, restaurants, repository);

        var result = await sut.Handle(Command(marketId, restaurantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
        repository.Routes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MarketMissingCoordinates_ReturnsValidationFailureAsync()
    {
        var marketId = Guid.NewGuid();
        var repository = new InMemoryDeliveryRouteRepository();
        var markets = Substitute.For<IMarketCoordinateReader>();
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        markets.FindByIdAsync(marketId, Arg.Any<CancellationToken>())
            .Returns(new MarketCoordinateDto(marketId, "Market A", null, 106.1m));
        var sut = new CalculateRouteCommandHandler(markets, restaurants, repository);

        var result = await sut.Handle(Command(marketId, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MISSING_COORDINATES");
        repository.Routes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RestaurantMissingCoordinates_ReturnsValidationFailureAsync()
    {
        var marketId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var repository = new InMemoryDeliveryRouteRepository();
        var markets = Substitute.For<IMarketCoordinateReader>();
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        markets.FindByIdAsync(marketId, Arg.Any<CancellationToken>())
            .Returns(new MarketCoordinateDto(marketId, "Market A", 10.1m, 106.1m));
        restaurants.FindByRestaurantIdAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantCoordinateDto(restaurantId, 10.2m, null));
        var sut = new CalculateRouteCommandHandler(markets, restaurants, repository);

        var result = await sut.Handle(Command(marketId, restaurantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MISSING_COORDINATES");
        repository.Routes.Should().BeEmpty();
    }

    private static CalculateRouteCommand Command(Guid marketId, Guid restaurantId) =>
        new([marketId], [], [restaurantId], "COST", new DateOnly(2026, 7, 9), false);
}
