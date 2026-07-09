using System.Reflection;
using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Commands.OptimizeRoute;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class OptimizeRouteCommandHandlerTests
{
    [Fact]
    public async Task Handle_PlannedRoute_AppliesOptimizationAndReturnsDtoAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = CreateRoute();
        await repository.AddAsync(route, default);
        var optimizer = Substitute.For<IRouteOptimizer>();
        var optimizedStops = route.Stops.Reverse().Select((stop, index) => stop with { StopOrder = index }).ToList();
        optimizer.Optimize(route.Stops, route.ServiceDate, OptimizationCriteria.distance)
            .Returns(new RouteOptimizationResult(optimizedStops, 10.5m, 21, 52500m));
        var sut = new OptimizeRouteCommandHandler(repository, optimizer);

        var result = await sut.Handle(new OptimizeRouteCommand(route.Id, "DISTANCE"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalDistanceKm.Should().Be(10.5m);
        result.Value.EstimatedDurationMinutes.Should().Be(21);
        result.Value.EstimatedCost.Should().Be(52500m);
        result.Value.OptimizationCriteria.Should().Be("distance");
        route.OptimizationCriteria.Should().Be(OptimizationCriteria.distance);
        route.Stops.Should().Equal(optimizedStops);
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RouteMissing_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var optimizer = Substitute.For<IRouteOptimizer>();
        var routeId = Guid.NewGuid();
        var sut = new OptimizeRouteCommandHandler(repository, optimizer);

        var result = await sut.Handle(new OptimizeRouteCommand(routeId, "COST"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RouteInvalidState_ReturnsConflictAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = CreateRoute();
        SetStatus(route, RouteStatus.reviewed);
        await repository.AddAsync(route, default);
        var optimizer = Substitute.For<IRouteOptimizer>();
        optimizer.Optimize(route.Stops, route.ServiceDate, OptimizationCriteria.cost)
            .Returns(new RouteOptimizationResult(route.Stops, 10m, 20, 50000m));
        var sut = new OptimizeRouteCommandHandler(repository, optimizer);

        var result = await sut.Handle(new OptimizeRouteCommand(route.Id, "cost"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ROUTE_INVALID_TRANSITION");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_InvalidCriteria_ReturnsValidationFailureAsync()
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = CreateRoute();
        await repository.AddAsync(route, default);
        var optimizer = Substitute.For<IRouteOptimizer>();
        var sut = new OptimizeRouteCommandHandler(repository, optimizer);

        var result = await sut.Handle(new OptimizeRouteCommand(route.Id, "FASTEST"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
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

    private static void SetStatus(DeliveryRoute route, RouteStatus status)
    {
        var field = typeof(DeliveryRoute).GetField(
            $"<{nameof(DeliveryRoute.Status)}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(route, status);
    }
}
