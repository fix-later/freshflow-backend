using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Queries.GetLoadingManifest;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetLoadingManifestQueryHandlerTests
{
    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid NearRestaurantId = Guid.NewGuid();
    private static readonly Guid FarRestaurantId = Guid.NewGuid();
    private static readonly Guid EmptyRestaurantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_MissingRoute_ReturnsNotFoundAsync()
    {
        var sut = new GetLoadingManifestQueryHandler(
            new InMemoryDeliveryRouteRepository(),
            Substitute.For<IDeliveryRepository>(),
            new InMemoryOrderStatusReader(),
            Substitute.For<IOrderPackingReader>());

        var result = await sut.Handle(new GetLoadingManifestQuery(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ReturnsAtHubGoodsPerStop_InLoadingOrderAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var route = CreateRoute();
        await routes.AddAsync(route, default);

        var nearOrderId = Guid.NewGuid();
        var farOrderId = Guid.NewGuid();

        var orders = new InMemoryOrderStatusReader();
        orders.Add(nearOrderId, "AtHub", NearRestaurantId);
        orders.Add(farOrderId, "AtHub", FarRestaurantId);
        orders.Add(Guid.NewGuid(), "Confirmed", NearRestaurantId); // not AtHub -> excluded

        var packing = Substitute.For<IOrderPackingReader>();
        packing.GetLinesByOrdersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<OrderPackingLines>
            {
                new(nearOrderId, [new OrderPackingLine(nearOrderId, Guid.NewGuid(), "Tomato", 5, 10m)]),
                new(farOrderId, [new OrderPackingLine(farOrderId, Guid.NewGuid(), "Fish", 3, 15m)]),
            });

        var sut = new GetLoadingManifestQueryHandler(
            routes, Substitute.For<IDeliveryRepository>(), orders, packing);

        var result = await sut.Handle(new GetLoadingManifestQuery(route.Id), default);

        result.IsSuccess.Should().BeTrue();
        // Loading order: furthest (StopOrder 2) first, then near (StopOrder 1). Empty stop dropped.
        result.Value.Stops.Should().HaveCount(2);
        result.Value.Stops[0].StopOrder.Should().Be(2);
        result.Value.Stops[0].RestaurantName.Should().Be("Far");
        result.Value.Stops[0].Lines.Should().ContainSingle(l => l.ProductName == "Fish" && l.OrderId == farOrderId);
        result.Value.Stops[1].StopOrder.Should().Be(1);
        result.Value.Stops[1].Lines.Should().ContainSingle(l => l.ProductName == "Tomato" && l.OrderId == nearOrderId);
    }

    private static DeliveryRoute CreateRoute() =>
        DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 25),
            [
                new RouteStop(0, StopEntityType.market, MarketId, "Market", 10.0m, 106.0m, null, null),
                new RouteStop(1, StopEntityType.restaurant, NearRestaurantId, "Near", 10.1m, 106.1m, null, null),
                new RouteStop(2, StopEntityType.restaurant, FarRestaurantId, "Far", 10.2m, 106.2m, null, null),
                new RouteStop(3, StopEntityType.restaurant, EmptyRestaurantId, "Empty", 10.3m, 106.3m, null, null),
            ],
            null);
}
