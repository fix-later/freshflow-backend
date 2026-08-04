using System.Reflection;
using FluentAssertions;
using FreshFlow.Logistics.Application.Queries.GetDriverRoutesToday;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetDriverRoutesTodayQueryHandlerTests
{
    private static readonly DateOnly TodayVn = new(2026, 7, 11);
    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 7, 10, 18, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Handle_DriverHasAssignedRouteToday_ReturnsRouteWithDeliveriesAsync()
    {
        var driverId = Guid.NewGuid();
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository();
        var route = CreateAssignedRoute(driverId, TodayVn);
        var firstOrderId = Guid.NewGuid();
        var secondOrderId = Guid.NewGuid();
        await routes.AddAsync(route, default);
        await deliveries.AddRangeAsync(
            [
                Delivery.Create(route.Id, secondOrderId, 2),
                Delivery.Create(route.Id, firstOrderId, 1)
            ],
            default);
        var sut = new GetDriverRoutesTodayQueryHandler(routes, deliveries, Clock);

        var result = await sut.Handle(new GetDriverRoutesTodayQuery(driverId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        var dto = result.Value[0];
        dto.RouteId.Should().Be(route.Id);
        dto.ServiceDate.Should().Be(TodayVn);
        dto.Status.Should().Be(RouteStatus.assigned.ToString());
        dto.Stops.Should().HaveCount(2);
        dto.Deliveries.Select(delivery => delivery.OrderId).Should().Equal(firstOrderId, secondOrderId);
        dto.Deliveries.Select(delivery => delivery.SequenceNumber).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Handle_RouteAssignedToAnotherDriver_HidesRouteAsync()
    {
        var driverId = Guid.NewGuid();
        var routes = new InMemoryDeliveryRouteRepository();
        await routes.AddAsync(CreateAssignedRoute(Guid.NewGuid(), TodayVn), default);
        var sut = new GetDriverRoutesTodayQueryHandler(routes, new InMemoryDeliveryRepository(), Clock);

        var result = await sut.Handle(new GetDriverRoutesTodayQuery(driverId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ServiceDateProvided_ReturnsRouteForThatFutureDayAsync()
    {
        var driverId = Guid.NewGuid();
        var futureDate = TodayVn.AddDays(3);
        var routes = new InMemoryDeliveryRouteRepository();
        await routes.AddAsync(CreateAssignedRoute(driverId, futureDate), default);
        await routes.AddAsync(CreateAssignedRoute(driverId, TodayVn), default);
        var sut = new GetDriverRoutesTodayQueryHandler(routes, new InMemoryDeliveryRepository(), Clock);

        var result = await sut.Handle(new GetDriverRoutesTodayQuery(driverId, futureDate), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(dto => dto.ServiceDate == futureDate);
    }

    [Fact]
    public async Task Handle_RouteOnAnotherDate_HidesRouteAsync()
    {
        var driverId = Guid.NewGuid();
        var routes = new InMemoryDeliveryRouteRepository();
        await routes.AddAsync(CreateAssignedRoute(driverId, TodayVn.AddDays(1)), default);
        var sut = new GetDriverRoutesTodayQueryHandler(routes, new InMemoryDeliveryRepository(), Clock);

        var result = await sut.Handle(new GetDriverRoutesTodayQuery(driverId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RouteNotAssigned_HidesRouteAsync()
    {
        var driverId = Guid.NewGuid();
        var routes = new InMemoryDeliveryRouteRepository();
        var route = CreateAssignedRoute(driverId, TodayVn);
        SetRouteStatus(route, RouteStatus.reviewed);
        await routes.AddAsync(route, default);
        var sut = new GetDriverRoutesTodayQueryHandler(routes, new InMemoryDeliveryRepository(), Clock);

        var result = await sut.Handle(new GetDriverRoutesTodayQuery(driverId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RouteInProgressToday_ReturnsRouteAsync()
    {
        var driverId = Guid.NewGuid();
        var routes = new InMemoryDeliveryRouteRepository();
        var route = CreateAssignedRoute(driverId, TodayVn);
        route.Start();
        await routes.AddAsync(route, default);
        var sut = new GetDriverRoutesTodayQueryHandler(routes, new InMemoryDeliveryRepository(), Clock);

        var result = await sut.Handle(new GetDriverRoutesTodayQuery(driverId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(dto =>
            dto.RouteId == route.Id &&
            dto.Status == RouteStatus.in_progress.ToString());
    }

    [Fact]
    public async Task Handle_NoRoutes_ReturnsEmptyListAndSkipsDeliveryLookupAsync()
    {
        var deliveries = new InMemoryDeliveryRepository();
        var sut = new GetDriverRoutesTodayQueryHandler(
            new InMemoryDeliveryRouteRepository(),
            deliveries,
            Clock);

        var result = await sut.Handle(new GetDriverRoutesTodayQuery(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        deliveries.GetByRouteIdsCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MultipleRoutes_LoadsDeliveriesInOneQueryAsync()
    {
        var driverId = Guid.NewGuid();
        var routes = new InMemoryDeliveryRouteRepository();
        var deliveries = new InMemoryDeliveryRepository();
        var firstRoute = CreateAssignedRoute(driverId, TodayVn);
        var secondRoute = CreateAssignedRoute(driverId, TodayVn);
        await routes.AddAsync(firstRoute, default);
        await routes.AddAsync(secondRoute, default);
        await deliveries.AddRangeAsync(
            [
                Delivery.Create(firstRoute.Id, Guid.NewGuid(), 1),
                Delivery.Create(secondRoute.Id, Guid.NewGuid(), 1)
            ],
            default);
        var sut = new GetDriverRoutesTodayQueryHandler(routes, deliveries, Clock);

        var result = await sut.Handle(new GetDriverRoutesTodayQuery(driverId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(route => route.Deliveries.Count == 1);
        deliveries.GetByRouteIdsCount.Should().Be(1);
        deliveries.LastRouteIds.Should().BeEquivalentTo(new[] { firstRoute.Id, secondRoute.Id });
    }

    private static DeliveryRoute CreateAssignedRoute(Guid driverId, DateOnly serviceDate)
    {
        var route = DeliveryRoute.CreateDirect(serviceDate, CreateStops(), null);
        route.ApplyOptimization(route.Stops, 12.3m, 30, 50000m, OptimizationCriteria.distance);
        route.Select();
        route.MarkReviewed();
        route.Assign(Guid.NewGuid(), driverId);
        return route;
    }

    private static IReadOnlyList<RouteStop> CreateStops() =>
        [
            new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
            new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
        ];

    private static void SetRouteStatus(DeliveryRoute route, RouteStatus status)
    {
        var field = typeof(DeliveryRoute).GetField(
            "<Status>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(route, status);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
