using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Commands.ReviewRoute;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ReviewRouteCommandHandlerTests
{
    [Fact]
    public async Task Handle_NoStopOrder_ReviewsRouteWithoutChangingMetricsAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = CreateOptimizedSelectedRoute();
        await repository.AddAsync(route, default);
        var optimizer = Substitute.For<IRouteOptimizer>();
        var sut = new ReviewRouteCommandHandler(repository, optimizer);

        var result = await sut.Handle(new ReviewRouteCommand(route.Id, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("reviewed");
        result.Value.TotalDistanceKm.Should().Be(12.34m);
        result.Value.EstimatedDurationMinutes.Should().Be(25);
        result.Value.EstimatedCost.Should().Be(61700m);
        route.Status.Should().Be(RouteStatus.reviewed);
        optimizer.DidNotReceiveWithAnyArgs().Recalculate(default!, default);
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ValidStopOrder_ReordersAndRecalculatesWithoutOptimizingAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = CreateOptimizedSelectedRoute();
        var stopIds = route.Stops.Select(stop => stop.EntityId).ToArray();
        var order = new[] { stopIds[0], stopIds[2], stopIds[1] };
        var recalculatedStops = order
            .Select((id, index) => route.Stops.Single(stop => stop.EntityId == id) with { StopOrder = index })
            .ToList();
        await repository.AddAsync(route, default);
        var optimizer = Substitute.For<IRouteOptimizer>();
        optimizer.Recalculate(
                Arg.Is<IReadOnlyList<RouteStop>>(stops => stops.Select(stop => stop.EntityId).SequenceEqual(order)),
                route.ServiceDate)
            .Returns(new RouteOptimizationResult(recalculatedStops, 8.9m, 18, 44500m));
        var sut = new ReviewRouteCommandHandler(repository, optimizer);

        var result = await sut.Handle(new ReviewRouteCommand(route.Id, order), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("reviewed");
        result.Value.Stops.Select(stop => stop.EntityId).Should().Equal(order);
        result.Value.TotalDistanceKm.Should().Be(8.9m);
        result.Value.EstimatedDurationMinutes.Should().Be(18);
        result.Value.EstimatedCost.Should().Be(44500m);
        optimizer.Received(1).Recalculate(
            Arg.Is<IReadOnlyList<RouteStop>>(stops => stops.Select(stop => stop.EntityId).SequenceEqual(order)),
            route.ServiceDate);
        optimizer.DidNotReceiveWithAnyArgs().Optimize(default!, default, default);
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_InvalidStopOrder_ReturnsValidationFailureAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = CreateOptimizedSelectedRoute();
        await repository.AddAsync(route, default);
        var optimizer = Substitute.For<IRouteOptimizer>();
        var duplicateId = route.Stops[0].EntityId;
        var sut = new ReviewRouteCommandHandler(repository, optimizer);

        var result = await sut.Handle(new ReviewRouteCommand(route.Id, [duplicateId, duplicateId]), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_STOP_ORDER");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RouteWithoutOptimization_ReturnsConflictAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = CreateSelectedRoute();
        await repository.AddAsync(route, default);
        var optimizer = Substitute.For<IRouteOptimizer>();
        var sut = new ReviewRouteCommandHandler(repository, optimizer);

        var result = await sut.Handle(new ReviewRouteCommand(route.Id, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ROUTE_INVALID_TRANSITION");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RouteMissing_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var optimizer = Substitute.For<IRouteOptimizer>();
        var routeId = Guid.NewGuid();
        var sut = new ReviewRouteCommandHandler(repository, optimizer);

        var result = await sut.Handle(new ReviewRouteCommand(routeId, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
        repository.SaveChangesCount.Should().Be(0);
    }

    private static DeliveryRoute CreateOptimizedSelectedRoute()
    {
        var route = CreateSelectedRoute();
        route.ApplyOptimization(route.Stops, 12.34m, 25, 61700m, OptimizationCriteria.cost);
        return route;
    }

    private static DeliveryRoute CreateSelectedRoute()
    {
        var route = DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 9),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant A", 10.2m, 106.2m, null, null),
                new RouteStop(2, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant B", 10.3m, 106.3m, null, null)
            ],
            null);
        route.Select();
        return route;
    }
}
