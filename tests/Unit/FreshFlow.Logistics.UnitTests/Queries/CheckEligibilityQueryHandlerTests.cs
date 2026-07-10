using System.Reflection;
using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Queries.CheckEligibility;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class CheckEligibilityQueryHandlerTests
{
    [Fact]
    public async Task Handle_RouteMissing_ReturnsNotFoundAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var vehicles = new InMemoryVehicleRepository();
        var drivers = Substitute.For<IDriverReader>();
        var sut = CreateSut(routes, vehicles, drivers);
        var routeId = Guid.NewGuid();

        var result = await sut.Handle(new CheckEligibilityQuery(routeId, Guid.NewGuid(), null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_VehicleMissing_ReturnsVehicleNotFoundOnlyAsync()
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var route = CreateRoute(stopCount: 3);
        await routes.AddAsync(route, default);
        var missingVehicleId = Guid.NewGuid();
        var otherRoute = CreateRoute(stopCount: 2);
        SetVehicle(otherRoute, missingVehicleId);
        await routes.AddAsync(otherRoute, default);
        var vehicles = new InMemoryVehicleRepository();
        var drivers = Substitute.For<IDriverReader>();
        var sut = CreateSut(routes, vehicles, drivers, maxStopsPerVehicle: 1);

        var result = await sut.Handle(new CheckEligibilityQuery(route.Id, missingVehicleId, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEligible.Should().BeFalse();
        result.Value.Reasons.Should().Equal("VEHICLE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_InactiveVehicle_ReturnsVehicleInactiveAsync()
    {
        var route = CreateRoute();
        var vehicle = new Vehicle("51A-12345", 1000m, VehicleType.truck, null);
        vehicle.Deactivate();
        var (sut, _) = await CreateHandlerWithRouteAndVehicleAsync(route, vehicle);

        var result = await sut.Handle(new CheckEligibilityQuery(route.Id, vehicle.Id, null), default);

        result.Value.IsEligible.Should().BeFalse();
        result.Value.Reasons.Should().Contain("VEHICLE_INACTIVE");
    }

    [Fact]
    public async Task Handle_UnavailableVehicle_ReturnsVehicleUnavailableAsync()
    {
        var route = CreateRoute();
        var vehicle = new Vehicle("51A-12345", 1000m, VehicleType.truck, null);
        vehicle.MarkUnavailable();
        var (sut, _) = await CreateHandlerWithRouteAndVehicleAsync(route, vehicle);

        var result = await sut.Handle(new CheckEligibilityQuery(route.Id, vehicle.Id, null), default);

        result.Value.IsEligible.Should().BeFalse();
        result.Value.Reasons.Should().Contain("VEHICLE_UNAVAILABLE");
    }

    [Theory]
    [InlineData(2, 2, false)]
    [InlineData(3, 2, true)]
    public async Task Handle_CapacityCheck_ComparesAgainstPolicyMaxStopsAsync(
        int stopCount,
        int maxStopsPerVehicle,
        bool expectCapacityExceeded)
    {
        var route = CreateRoute(stopCount);
        var vehicle = new Vehicle("51A-12345", 1000m, VehicleType.truck, null);
        var (sut, _) = await CreateHandlerWithRouteAndVehicleAsync(
            route,
            vehicle,
            maxStopsPerVehicle);

        var result = await sut.Handle(new CheckEligibilityQuery(route.Id, vehicle.Id, null), default);

        result.Value.Reasons.Contains("VEHICLE_CAPACITY_EXCEEDED").Should().Be(expectCapacityExceeded);
    }

    [Theory]
    [InlineData(RouteStatus.assigned, 0, true)]
    [InlineData(RouteStatus.cancelled, 0, false)]
    [InlineData(RouteStatus.assigned, 1, false)]
    public async Task Handle_DoubleBookRules_RespectStatusAndServiceDateAsync(
        RouteStatus otherRouteStatus,
        int otherRouteDateOffset,
        bool expectDoubleBooked)
    {
        var routes = new InMemoryDeliveryRouteRepository();
        var route = CreateRoute();
        var vehicle = new Vehicle("51A-12345", 1000m, VehicleType.truck, null);
        await routes.AddAsync(route, default);
        var otherRoute = CreateRoute(serviceDate: route.ServiceDate.AddDays(otherRouteDateOffset));
        SetVehicle(otherRoute, vehicle.Id);
        SetStatus(otherRoute, otherRouteStatus);
        await routes.AddAsync(otherRoute, default);
        var vehicles = new InMemoryVehicleRepository();
        await vehicles.AddAsync(vehicle, default);
        var drivers = Substitute.For<IDriverReader>();
        var sut = CreateSut(routes, vehicles, drivers);

        var result = await sut.Handle(new CheckEligibilityQuery(route.Id, vehicle.Id, null), default);

        result.Value.Reasons.Contains("VEHICLE_DOUBLE_BOOKED").Should().Be(expectDoubleBooked);
    }

    [Fact]
    public async Task Handle_NullDriverUserId_SkipsDriverRulesAsync()
    {
        var route = CreateRoute();
        var vehicle = new Vehicle("51A-12345", 1000m, VehicleType.truck, null);
        var drivers = Substitute.For<IDriverReader>();
        var (sut, _) = await CreateHandlerWithRouteAndVehicleAsync(route, vehicle, drivers: drivers);

        var result = await sut.Handle(new CheckEligibilityQuery(route.Id, vehicle.Id, null), default);

        result.Value.Reasons.Should().NotContain(reason => reason.StartsWith("DRIVER_"));
        _ = drivers.DidNotReceiveWithAnyArgs().FindByUserIdAsync(default, default);
    }

    [Fact]
    public async Task Handle_DriverMissing_ReturnsDriverNotFoundAsync()
    {
        var route = CreateRoute();
        var vehicle = new Vehicle("51A-12345", 1000m, VehicleType.truck, null);
        var driverUserId = Guid.NewGuid();
        var drivers = Substitute.For<IDriverReader>();
        var (sut, _) = await CreateHandlerWithRouteAndVehicleAsync(route, vehicle, drivers: drivers);

        var result = await sut.Handle(new CheckEligibilityQuery(route.Id, vehicle.Id, driverUserId), default);

        result.Value.IsEligible.Should().BeFalse();
        result.Value.Reasons.Should().Contain("DRIVER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_DriverWrongRole_ReturnsDriverNotDriverRoleAsync()
    {
        var route = CreateRoute();
        var vehicle = new Vehicle("51A-12345", 1000m, VehicleType.truck, null);
        var driverUserId = Guid.NewGuid();
        var drivers = Substitute.For<IDriverReader>();
        drivers.FindByUserIdAsync(driverUserId, Arg.Any<CancellationToken>())
            .Returns(new DriverDto(driverUserId, "admin", true));
        var (sut, _) = await CreateHandlerWithRouteAndVehicleAsync(route, vehicle, drivers: drivers);

        var result = await sut.Handle(new CheckEligibilityQuery(route.Id, vehicle.Id, driverUserId), default);

        result.Value.IsEligible.Should().BeFalse();
        result.Value.Reasons.Should().Contain("DRIVER_NOT_DRIVER_ROLE");
    }

    [Fact]
    public async Task Handle_InactiveDriver_ReturnsDriverInactiveAsync()
    {
        var route = CreateRoute();
        var vehicle = new Vehicle("51A-12345", 1000m, VehicleType.truck, null);
        var driverUserId = Guid.NewGuid();
        var drivers = Substitute.For<IDriverReader>();
        drivers.FindByUserIdAsync(driverUserId, Arg.Any<CancellationToken>())
            .Returns(new DriverDto(driverUserId, "driver", false));
        var (sut, _) = await CreateHandlerWithRouteAndVehicleAsync(route, vehicle, drivers: drivers);

        var result = await sut.Handle(new CheckEligibilityQuery(route.Id, vehicle.Id, driverUserId), default);

        result.Value.IsEligible.Should().BeFalse();
        result.Value.Reasons.Should().Contain("DRIVER_INACTIVE");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_HappyPath_ReturnsEligibleAsync(bool includeDriver)
    {
        var route = CreateRoute();
        var vehicle = new Vehicle("51A-12345", 1000m, VehicleType.truck, null);
        var driverUserId = includeDriver ? Guid.NewGuid() : (Guid?)null;
        var drivers = Substitute.For<IDriverReader>();
        if (driverUserId.HasValue)
        {
            drivers.FindByUserIdAsync(driverUserId.Value, Arg.Any<CancellationToken>())
                .Returns(new DriverDto(driverUserId.Value, "driver", true));
        }

        var (sut, _) = await CreateHandlerWithRouteAndVehicleAsync(route, vehicle, drivers: drivers);

        var result = await sut.Handle(new CheckEligibilityQuery(route.Id, vehicle.Id, driverUserId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEligible.Should().BeTrue();
        result.Value.Reasons.Should().BeEmpty();
    }

    private static CheckEligibilityQueryHandler CreateSut(
        InMemoryDeliveryRouteRepository routes,
        InMemoryVehicleRepository vehicles,
        IDriverReader drivers,
        int maxStopsPerVehicle = 20)
    {
        var capacityPolicy = Substitute.For<IVehicleCapacityPolicy>();
        capacityPolicy.MaxStopsPerVehicle.Returns(maxStopsPerVehicle);

        return new CheckEligibilityQueryHandler(routes, vehicles, drivers, capacityPolicy);
    }

    private static async Task<(CheckEligibilityQueryHandler Handler, IDriverReader Drivers)> CreateHandlerWithRouteAndVehicleAsync(
        DeliveryRoute route,
        Vehicle vehicle,
        int maxStopsPerVehicle = 20)
    {
        var drivers = Substitute.For<IDriverReader>();
        return await CreateHandlerWithRouteAndVehicleAsync(route, vehicle, drivers, maxStopsPerVehicle);
    }

    private static async Task<(CheckEligibilityQueryHandler Handler, IDriverReader Drivers)> CreateHandlerWithRouteAndVehicleAsync(
        DeliveryRoute route,
        Vehicle vehicle,
        IDriverReader drivers,
        int maxStopsPerVehicle = 20)
    {
        var routes = new InMemoryDeliveryRouteRepository();
        await routes.AddAsync(route, default);
        var vehicles = new InMemoryVehicleRepository();
        await vehicles.AddAsync(vehicle, default);

        return (CreateSut(routes, vehicles, drivers, maxStopsPerVehicle), drivers);
    }

    private static DeliveryRoute CreateRoute(int stopCount = 2, DateOnly? serviceDate = null)
    {
        var stops = new List<RouteStop>
        {
            new(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null)
        };

        for (var i = 1; i < stopCount; i++)
        {
            stops.Add(new RouteStop(
                i,
                StopEntityType.restaurant,
                Guid.NewGuid(),
                $"Restaurant {i}",
                10.1m + i / 100m,
                106.1m + i / 100m,
                null,
                null));
        }

        return DeliveryRoute.CreateDirect(serviceDate ?? new DateOnly(2026, 7, 9), stops, null);
    }

    private static void SetVehicle(DeliveryRoute route, Guid vehicleId)
    {
        var field = typeof(DeliveryRoute).GetField(
            $"<{nameof(DeliveryRoute.VehicleId)}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(route, vehicleId);
    }

    private static void SetStatus(DeliveryRoute route, RouteStatus status)
    {
        var field = typeof(DeliveryRoute).GetField(
            $"<{nameof(DeliveryRoute.Status)}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(route, status);
    }
}
