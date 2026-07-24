using System.Reflection;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Logistics.Application.Commands.AssignVehicle;
using FreshFlow.Logistics.Application.Commands.CalculateRoute;
using FreshFlow.Logistics.Application.Commands.OptimizeRoute;
using FreshFlow.Logistics.Application.Commands.ReviewRoute;
using FreshFlow.Logistics.Application.Commands.SelectRoute;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Queries.CheckEligibility;
using FreshFlow.Logistics.Application.Queries.GetRoute;
using FreshFlow.Logistics.Application.Queries.ListRoutes;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class RoutesControllerTests
{
    [Fact]
    public void RoutesController_AllowsHubStaffReadOnly_WritesNarrowedToAdminAndOps()
    {
        var classAttr = typeof(RoutesController).GetCustomAttribute<AuthorizeAttribute>();
        classAttr.Should().NotBeNull();
        classAttr!.Roles.Should().Be("admin,operations_manager,hub_staff");

        var writes = new[]
        {
            nameof(RoutesController.CalculateRouteAsync),
            nameof(RoutesController.SelectRouteAsync),
            nameof(RoutesController.OptimizeRouteAsync),
            nameof(RoutesController.ReviewRouteAsync),
            nameof(RoutesController.AssignVehicleAsync),
        };

        foreach (var write in writes)
        {
            var methodAttr = typeof(RoutesController).GetMethod(write)!.GetCustomAttribute<AuthorizeAttribute>();
            methodAttr.Should().NotBeNull($"{write} must exclude hub_staff");
            methodAttr!.Roles.Should().Be("admin,operations_manager");
        }
    }

    [Fact]
    public async Task CalculateRouteAsync_Success_SendsCommandAndReturnsCreatedAsync()
    {
        var sender = Substitute.For<ISender>();
        var dto = CreateDto();
        sender.Send(Arg.Any<CalculateRouteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDto>.Success(dto));
        var controller = new RoutesController(sender);
        var marketId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();

        var result = await controller.CalculateRouteAsync(
            new CalculateRouteRequest(
                [marketId],
                [hubId],
                [restaurantId],
                "COST",
                new DateOnly(2026, 7, 9),
                true),
            default);

        result.Should().BeOfType<CreatedAtActionResult>();
        await sender.Received(1).Send(
            Arg.Is<CalculateRouteCommand>(command =>
                command.SourceMarketIds.SequenceEqual(new[] { marketId }) &&
                command.HubIds.SequenceEqual(new[] { hubId }) &&
                command.DestinationRestaurantIds.SequenceEqual(new[] { restaurantId }) &&
                command.OptimizationCriteria == "COST" &&
                command.ServiceDate == new DateOnly(2026, 7, 9) &&
                command.CompareWithHub),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CalculateRouteAsync_HubRelayNotSupported_Returns422Async()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CalculateRouteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDto>.Failure(Error.Validation("HUB_RELAY_NOT_SUPPORTED", "hub")));
        var controller = new RoutesController(sender);

        var result = await controller.CalculateRouteAsync(
            new CalculateRouteRequest([Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()], null, new DateOnly(2026, 7, 9), null),
            default);

        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task SelectRouteAsync_InvalidTransition_Returns409Async()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<SelectRouteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDto>.Failure(Error.Conflict("ROUTE_INVALID_TRANSITION", "invalid")));
        var controller = new RoutesController(sender);

        var result = await controller.SelectRouteAsync(Guid.NewGuid(), default);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task OptimizeRouteAsync_Success_SendsCommandAsync()
    {
        var sender = Substitute.For<ISender>();
        var routeId = Guid.NewGuid();
        sender.Send(Arg.Any<OptimizeRouteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDto>.Success(CreateDto(routeId)));
        var controller = new RoutesController(sender);

        var result = await controller.OptimizeRouteAsync(
            routeId,
            new OptimizeRouteRequest("DISTANCE"),
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<OptimizeRouteCommand>(command =>
                command.RouteId == routeId &&
                command.OptimizationCriteria == "DISTANCE"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OptimizeRouteAsync_InvalidTransition_Returns409Async()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<OptimizeRouteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDto>.Failure(Error.Conflict("ROUTE_INVALID_TRANSITION", "invalid")));
        var controller = new RoutesController(sender);

        var result = await controller.OptimizeRouteAsync(
            Guid.NewGuid(),
            new OptimizeRouteRequest("COST"),
            default);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ReviewRouteAsync_NullBody_SendsCommandWithNullStopOrderAsync()
    {
        var sender = Substitute.For<ISender>();
        var routeId = Guid.NewGuid();
        sender.Send(Arg.Any<ReviewRouteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDto>.Success(CreateDto(routeId)));
        var controller = new RoutesController(sender);

        var result = await controller.ReviewRouteAsync(routeId, null, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ReviewRouteCommand>(command =>
                command.RouteId == routeId &&
                command.StopOrder == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewRouteAsync_InvalidStopOrder_Returns422Async()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ReviewRouteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDto>.Failure(Error.Validation("INVALID_STOP_ORDER", "invalid")));
        var controller = new RoutesController(sender);

        var result = await controller.ReviewRouteAsync(
            Guid.NewGuid(),
            new ReviewRouteRequest([Guid.NewGuid()]),
            default);

        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task ListRoutesAsync_Success_SendsQueryAsync()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ListRoutesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<RoutePageDto>.Success(new RoutePageDto([CreateDto()], 25, "next")));
        var controller = new RoutesController(sender);
        var serviceDate = new DateOnly(2026, 7, 9);

        var result = await controller.ListRoutesAsync("cursor", 25, serviceDate, "selected", default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ListRoutesQuery>(query =>
                query.Cursor == "cursor" &&
                query.PageSize == 25 &&
                query.ServiceDate == serviceDate &&
                query.Status == "selected"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckEligibilityAsync_Success_SendsQueryAsync()
    {
        var sender = Substitute.For<ISender>();
        var routeId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        sender.Send(Arg.Any<CheckEligibilityQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<EligibilityResultDto>.Success(new EligibilityResultDto(true, [])));
        var controller = new RoutesController(sender);

        var result = await controller.CheckEligibilityAsync(routeId, vehicleId, driverUserId, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<CheckEligibilityQuery>(query =>
                query.RouteId == routeId &&
                query.VehicleId == vehicleId &&
                query.DriverUserId == driverUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckEligibilityAsync_RouteNotFound_Returns404Async()
    {
        var sender = Substitute.For<ISender>();
        var routeId = Guid.NewGuid();
        sender.Send(Arg.Any<CheckEligibilityQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<EligibilityResultDto>.Failure(Error.NotFound("DELIVERY_ROUTE", routeId)));
        var controller = new RoutesController(sender);

        var result = await controller.CheckEligibilityAsync(routeId, Guid.NewGuid(), null, default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AssignVehicleAsync_Success_SendsCommandAsync()
    {
        var sender = Substitute.For<ISender>();
        var routeId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        sender.Send(Arg.Any<AssignVehicleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDto>.Success(CreateDto(routeId)));
        var controller = new RoutesController(sender);

        var result = await controller.AssignVehicleAsync(
            routeId,
            new AssignVehicleRequest(vehicleId, driverUserId),
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<AssignVehicleCommand>(command =>
                command.RouteId == routeId &&
                command.VehicleId == vehicleId &&
                command.DriverUserId == driverUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignVehicleAsync_NotEligible_Returns422Async()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<AssignVehicleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDto>.Failure(Error.Validation("VEHICLE_NOT_ELIGIBLE", "invalid")));
        var controller = new RoutesController(sender);

        var result = await controller.AssignVehicleAsync(
            Guid.NewGuid(),
            new AssignVehicleRequest(Guid.NewGuid(), null),
            default);

        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task AssignVehicleAsync_NotAvailable_Returns409Async()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<AssignVehicleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDto>.Failure(Error.Conflict("VEHICLE_NOT_AVAILABLE", "conflict")));
        var controller = new RoutesController(sender);

        var result = await controller.AssignVehicleAsync(
            Guid.NewGuid(),
            new AssignVehicleRequest(Guid.NewGuid(), null),
            default);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task GetRouteAsync_NotFound_Returns404Async()
    {
        var sender = Substitute.For<ISender>();
        var routeId = Guid.NewGuid();
        sender.Send(Arg.Any<GetRouteQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<RouteDto>.Failure(Error.NotFound("DELIVERY_ROUTE", routeId)));
        var controller = new RoutesController(sender);

        var result = await controller.GetRouteAsync(routeId, default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    private static RouteDto CreateDto(Guid? id = null) =>
        new(
            id ?? Guid.NewGuid(),
            "direct",
            "planned",
            new DateOnly(2026, 7, 9),
            [
                new RouteStopDto(0, "market", Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStopDto(1, "restaurant", Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
            ],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);
}
