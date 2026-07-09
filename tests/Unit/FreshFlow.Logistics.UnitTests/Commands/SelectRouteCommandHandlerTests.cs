using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.SelectRoute;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class SelectRouteCommandHandlerTests
{
    [Fact]
    public async Task Handle_PlannedRoute_SelectsRouteAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = CreateRoute();
        await repository.AddAsync(route, default);
        var sut = new SelectRouteCommandHandler(repository);

        var result = await sut.Handle(new SelectRouteCommand(route.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("selected");
        route.Status.Should().Be(RouteStatus.selected);
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RouteMissing_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var routeId = Guid.NewGuid();
        var sut = new SelectRouteCommandHandler(repository);

        var result = await sut.Handle(new SelectRouteCommand(routeId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RouteNotPlanned_ReturnsConflictAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = CreateRoute();
        route.Select();
        await repository.AddAsync(route, default);
        var sut = new SelectRouteCommandHandler(repository);

        var result = await sut.Handle(new SelectRouteCommand(route.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ROUTE_INVALID_TRANSITION");
        repository.SaveChangesCount.Should().Be(0);
    }

    private static DeliveryRoute CreateRoute() =>
        DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 9),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
            ],
            null);
}
