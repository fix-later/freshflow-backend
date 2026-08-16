using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.AttachProofOfDelivery;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class AttachProofOfDeliveryCommandHandlerTests
{
    [Fact]
    public async Task Handle_OwnerDriverWithValidUrl_AttachesProofAndSavesAsync()
    {
        var driverId = Guid.NewGuid();
        var route = AssignedRoute(driverId);
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var deliveries = new InMemoryDeliveryRepository();
        var routes = new InMemoryDeliveryRouteRepository();
        await deliveries.AddRangeAsync([delivery], default);
        await routes.AddAsync(route, default);
        var sut = new AttachProofOfDeliveryCommandHandler(deliveries, routes);

        var result = await sut.Handle(
            new AttachProofOfDeliveryCommand(
                delivery.Id,
                driverId,
                "https://res.cloudinary.com/demo/image/upload/pod.jpg"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeliveryId.Should().Be(delivery.Id);
        result.Value.ProofUrl.Should().Be("https://res.cloudinary.com/demo/image/upload/pod.jpg");
        delivery.ProofUrl.Should().Be("https://res.cloudinary.com/demo/image/upload/pod.jpg");
        deliveries.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DeliveredDelivery_AttachesProofAsync()
    {
        var driverId = Guid.NewGuid();
        var route = AssignedRoute(driverId);
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        delivery.MarkDelivered(DateTime.UtcNow);
        var deliveries = new InMemoryDeliveryRepository();
        var routes = new InMemoryDeliveryRouteRepository();
        await deliveries.AddRangeAsync([delivery], default);
        await routes.AddAsync(route, default);
        var sut = new AttachProofOfDeliveryCommandHandler(deliveries, routes);

        var result = await sut.Handle(
            new AttachProofOfDeliveryCommand(
                delivery.Id,
                driverId,
                "https://res.cloudinary.com/demo/image/upload/pod.jpg"),
            default);

        result.IsSuccess.Should().BeTrue();
        delivery.ProofUrl.Should().Be("https://res.cloudinary.com/demo/image/upload/pod.jpg");
    }

    [Fact]
    public async Task Handle_DeliveryMissing_ReturnsNotFoundAsync()
    {
        var sut = new AttachProofOfDeliveryCommandHandler(
            new InMemoryDeliveryRepository(),
            new InMemoryDeliveryRouteRepository());

        var result = await sut.Handle(
            new AttachProofOfDeliveryCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "https://res.cloudinary.com/demo/image/upload/pod.jpg"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_DifferentDriver_ReturnsForbiddenAndDoesNotSaveAsync()
    {
        var route = AssignedRoute(Guid.NewGuid());
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var deliveries = new InMemoryDeliveryRepository();
        var routes = new InMemoryDeliveryRouteRepository();
        await deliveries.AddRangeAsync([delivery], default);
        await routes.AddAsync(route, default);
        var sut = new AttachProofOfDeliveryCommandHandler(deliveries, routes);

        var result = await sut.Handle(
            new AttachProofOfDeliveryCommand(
                delivery.Id,
                Guid.NewGuid(),
                "https://res.cloudinary.com/demo/image/upload/pod.jpg"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        deliveries.SaveChangesCount.Should().Be(0);
    }

    private static DeliveryRoute AssignedRoute(Guid driverId)
    {
        var route = DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 12),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
            ],
            null);
        route.Select();
        route.ApplyOptimization(route.Stops, 12m, 30, 100m, OptimizationCriteria.cost);
        route.MarkReviewed();
        route.Assign(Guid.NewGuid(), driverId);
        return route;
    }
}
