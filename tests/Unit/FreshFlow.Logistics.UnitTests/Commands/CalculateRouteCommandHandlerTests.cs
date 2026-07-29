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
    public async Task Handle_HappyPath_PersistsHubRouteWithInputOrderAsync()
    {
        var hubId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var repository = new InMemoryDeliveryRouteRepository();
        var hubs = Substitute.For<IHubCoordinateReader>();
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        hubs.FindByIdAsync(hubId, Arg.Any<CancellationToken>())
            .Returns(new HubCoordinateDto(hubId, Guid.NewGuid(), "Hub A", 10.1m, 106.1m));
        restaurants.FindByRestaurantIdAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantCoordinateDto(restaurantId, "Bistro B", 10.2m, 106.2m));
        var sut = new CalculateRouteCommandHandler(hubs, restaurants, repository);

        var result = await sut.Handle(
            new CalculateRouteCommand(
                hubId,
                [restaurantId],
                null,
                new DateOnly(2026, 7, 9)),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.HubId.Should().Be(hubId);
        result.Value.RouteType.Should().Be("hub_relay");
        result.Value.Status.Should().Be("planned");
        result.Value.Stops.Should().HaveCount(2);
        result.Value.Stops[0].EntityType.Should().Be("hub");
        result.Value.Stops[0].EntityId.Should().Be(hubId);
        result.Value.Stops[0].EntityName.Should().Be("Hub A");
        result.Value.Stops[1].EntityType.Should().Be("restaurant");
        result.Value.Stops[1].EntityId.Should().Be(restaurantId);
        result.Value.Stops[1].EntityName.Should().Be("Bistro B");
        result.Value.Stops[1].EntityName.Should().NotBe(restaurantId.ToString());
        repository.Routes.Should().ContainSingle(route => route.RouteType == RouteType.hub_relay);
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_HubMissing_ReturnsNotFoundAsync()
    {
        var hubId = Guid.NewGuid();
        var repository = new InMemoryDeliveryRouteRepository();
        var hubs = Substitute.For<IHubCoordinateReader>();
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        var sut = new CalculateRouteCommandHandler(hubs, restaurants, repository);

        var result = await sut.Handle(Command(hubId, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
        repository.Routes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RestaurantMissing_ReturnsNotFoundAsync()
    {
        var hubId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var repository = new InMemoryDeliveryRouteRepository();
        var hubs = Substitute.For<IHubCoordinateReader>();
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        hubs.FindByIdAsync(hubId, Arg.Any<CancellationToken>())
            .Returns(new HubCoordinateDto(hubId, Guid.NewGuid(), "Hub A", 10.1m, 106.1m));
        var sut = new CalculateRouteCommandHandler(hubs, restaurants, repository);

        var result = await sut.Handle(Command(hubId, restaurantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
        repository.Routes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_HubMissingCoordinates_ReturnsValidationFailureAsync()
    {
        var hubId = Guid.NewGuid();
        var repository = new InMemoryDeliveryRouteRepository();
        var hubs = Substitute.For<IHubCoordinateReader>();
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        hubs.FindByIdAsync(hubId, Arg.Any<CancellationToken>())
            .Returns(new HubCoordinateDto(hubId, Guid.NewGuid(), "Hub A", null, 106.1m));
        var sut = new CalculateRouteCommandHandler(hubs, restaurants, repository);

        var result = await sut.Handle(Command(hubId, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MISSING_COORDINATES");
        repository.Routes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RestaurantMissingCoordinates_ReturnsValidationFailureAsync()
    {
        var hubId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var repository = new InMemoryDeliveryRouteRepository();
        var hubs = Substitute.For<IHubCoordinateReader>();
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        hubs.FindByIdAsync(hubId, Arg.Any<CancellationToken>())
            .Returns(new HubCoordinateDto(hubId, Guid.NewGuid(), "Hub A", 10.1m, 106.1m));
        restaurants.FindByRestaurantIdAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantCoordinateDto(restaurantId, "Bistro B", 10.2m, null));
        var sut = new CalculateRouteCommandHandler(hubs, restaurants, repository);

        var result = await sut.Handle(Command(hubId, restaurantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MISSING_COORDINATES");
        repository.Routes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MoreThan20Stops_ReturnsStopLimitExceededAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var hubs = Substitute.For<IHubCoordinateReader>();
        var restaurants = Substitute.For<IRestaurantCoordinateReader>();
        var sut = new CalculateRouteCommandHandler(hubs, restaurants, repository);
        var destinationRestaurantIds = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToList();

        var result = await sut.Handle(
            new CalculateRouteCommand(
                Guid.NewGuid(),
                destinationRestaurantIds,
                "COST",
                new DateOnly(2026, 7, 9)),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("STOP_LIMIT_EXCEEDED");
        repository.Routes.Should().BeEmpty();
    }

    private static CalculateRouteCommand Command(Guid hubId, Guid restaurantId) =>
        new(hubId, [restaurantId], "COST", new DateOnly(2026, 7, 9));
}
