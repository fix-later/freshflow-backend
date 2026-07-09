using FluentAssertions;
using FreshFlow.Logistics.Application.Queries.GetRoute;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetRouteQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingRoute_ReturnsDtoAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = CreateRoute();
        await repository.AddAsync(route, default);
        var sut = new GetRouteQueryHandler(repository);

        var result = await sut.Handle(new GetRouteQuery(route.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(route.Id);
        result.Value.Stops.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_MissingRoute_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var routeId = Guid.NewGuid();
        var sut = new GetRouteQueryHandler(repository);

        var result = await sut.Handle(new GetRouteQuery(routeId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
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
