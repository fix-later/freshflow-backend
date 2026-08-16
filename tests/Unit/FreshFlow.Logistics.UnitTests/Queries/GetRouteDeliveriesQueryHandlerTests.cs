using FluentAssertions;
using FreshFlow.Logistics.Application.Queries.GetRouteDeliveries;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetRouteDeliveriesQueryHandlerTests
{
    [Fact]
    public async Task ExistingRoute_ReturnsDeliveriesWithAndWithoutProofInSequenceAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository();
        var route = CreateRoute();
        var withProof = Delivery.Create(route.Id, Guid.NewGuid(), 2);
        withProof.AttachProof("https://res.cloudinary.com/demo/image/upload/pod.jpg");
        var withoutProof = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        await routes.AddAsync(route, default);
        await deliveries.AddRangeAsync([withProof, withoutProof], default);
        var sut = new GetRouteDeliveriesQueryHandler(routes, deliveries);

        var result = await sut.Handle(new GetRouteDeliveriesQuery(route.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(delivery => delivery.SequenceNumber).Should().Equal(1, 2);
        result.Value[0].ProofUrl.Should().BeNull();
        result.Value[1].ProofUrl.Should().Be(withProof.ProofUrl);
    }

    [Fact]
    public async Task ExistingRouteWithoutDeliveries_ReturnsEmptyListAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var route = CreateRoute();
        await routes.AddAsync(route, default);
        var sut = new GetRouteDeliveriesQueryHandler(routes, new InMemoryDeliveryRepository());

        var result = await sut.Handle(new GetRouteDeliveriesQuery(route.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingRoute_ReturnsNotFoundWithoutDeliveryLookupAsync()
    {
        var deliveries = new InMemoryDeliveryRepository();
        var routeId = Guid.NewGuid();
        var sut = new GetRouteDeliveriesQueryHandler(
            new InMemoryDeliveryRouteRepository(),
            deliveries);

        var result = await sut.Handle(new GetRouteDeliveriesQuery(routeId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
        deliveries.GetByRouteIdsCount.Should().Be(0);
    }

    private static DeliveryRoute CreateRoute() =>
        DeliveryRoute.CreateDirect(
            new DateOnly(2026, 8, 9),
            [
                new RouteStop(
                    0, StopEntityType.market, Guid.NewGuid(), "Market",
                    10.1m, 106.1m, null, null),
                new RouteStop(
                    1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant",
                    10.2m, 106.2m, null, null)
            ],
            null);
}
