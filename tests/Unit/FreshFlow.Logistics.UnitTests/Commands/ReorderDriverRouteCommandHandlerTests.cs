using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Commands.ReorderDriverRoute;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ReorderDriverRouteCommandHandlerTests
{
    [Fact]
    public async Task Handle_WrongDriver_ReturnsForbiddenAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var route = CreateAssignedRoute(Guid.NewGuid());
        await routes.AddAsync(route, default);
        var sut = new ReorderDriverRouteCommandHandler(
            routes, Substitute.For<IRouteOptimizer>(), Substitute.For<IHubSortingStateReader>());

        var result = await sut.Handle(
            new ReorderDriverRouteCommand(
                route.Id,
                Guid.NewGuid(),
                route.Stops.Select(stop => stop.EntityId).ToList()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        routes.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RouteNotAssigned_ReturnsNotReorderableAsync()
    {
        var driverId = Guid.NewGuid();
        var routes = new InMemoryDeliveryRouteRepository();
        var route = CreateAssignedRoute(driverId);
        route.Start();
        await routes.AddAsync(route, default);
        var sut = new ReorderDriverRouteCommandHandler(
            routes, Substitute.For<IRouteOptimizer>(), Substitute.For<IHubSortingStateReader>());

        var result = await sut.Handle(
            new ReorderDriverRouteCommand(
                route.Id,
                driverId,
                route.Stops.Select(stop => stop.EntityId).ToList()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ROUTE_NOT_REORDERABLE");
        routes.SaveChangesCount.Should().Be(0);
    }


    [Fact]
    public async Task Handle_SortedRoute_ReturnsLockedConflictAsync()
    {
        var driverId = Guid.NewGuid();
        var routes = new InMemoryDeliveryRouteRepository();
        var route = CreateAssignedRoute(driverId);
        await routes.AddAsync(route, default);
        var sorting = Substitute.For<IHubSortingStateReader>();
        sorting.HasSortedLinesAsync(
                route.Id,
                route.Stops[0].EntityId,
                route.ServiceDate,
                Arg.Any<CancellationToken>())
            .Returns(true);
        var optimizer = Substitute.For<IRouteOptimizer>();
        var sut = new ReorderDriverRouteCommandHandler(routes, optimizer, sorting);

        var result = await sut.Handle(
            new ReorderDriverRouteCommand(
                route.Id,
                driverId,
                route.Stops.Select(stop => stop.EntityId).ToList()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ROUTE_LOCKED_FOR_SORTING");
        routes.SaveChangesCount.Should().Be(0);
        _ = optimizer.DidNotReceiveWithAnyArgs().Recalculate(default!, default);
    }
    [Fact]
    public async Task Handle_AssignedRoute_ReordersAndRecalculatesAsync()
    {
        var driverId = Guid.NewGuid();
        var routes = new InMemoryDeliveryRouteRepository();
        var route = CreateAssignedRoute(driverId);
        var orderedIds = new[]
        {
            route.Stops[0].EntityId,
            route.Stops[2].EntityId,
            route.Stops[1].EntityId
        };
        var recalculatedStops = orderedIds
            .Select((id, index) => route.Stops.Single(stop => stop.EntityId == id) with { StopOrder = index })
            .ToList();
        await routes.AddAsync(route, default);
        var optimizer = Substitute.For<IRouteOptimizer>();
        optimizer.Recalculate(
                Arg.Is<IReadOnlyList<RouteStop>>(stops =>
                    stops.Select(stop => stop.EntityId).SequenceEqual(orderedIds)),
                route.ServiceDate)
            .Returns(new RouteOptimizationResult(recalculatedStops, 8.9m, 18, 44500m));
        var sorting = Substitute.For<IHubSortingStateReader>();
        var sut = new ReorderDriverRouteCommandHandler(routes, optimizer, sorting);

        var result = await sut.Handle(
            new ReorderDriverRouteCommand(route.Id, driverId, orderedIds),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("assigned");
        result.Value.Stops.Select(stop => stop.EntityId).Should().Equal(orderedIds);
        result.Value.TotalDistanceKm.Should().Be(8.9m);
        result.Value.EstimatedDurationMinutes.Should().Be(18);
        result.Value.EstimatedCost.Should().Be(44500m);
        result.Value.OptimizationCriteria.Should().Be("distance");
        optimizer.Received(1).Recalculate(
            Arg.Is<IReadOnlyList<RouteStop>>(stops =>
                stops.Select(stop => stop.EntityId).SequenceEqual(orderedIds)),
            route.ServiceDate);
        await sorting.Received(1).HasSortedLinesAsync(
            route.Id,
            route.Stops[0].EntityId,
            route.ServiceDate,
            Arg.Any<CancellationToken>());
        routes.SaveChangesCount.Should().Be(1);
    }

    private static DeliveryRoute CreateAssignedRoute(Guid driverId)
    {
        var route = DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 11),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant A", 10.2m, 106.2m, null, null),
                new RouteStop(2, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant B", 10.3m, 106.3m, null, null)
            ],
            null);
        route.ApplyOptimization(route.Stops, 12.3m, 30, 50000m, OptimizationCriteria.distance);
        route.Select();
        route.MarkReviewed();
        route.Assign(Guid.NewGuid(), driverId);
        return route;
    }
}
