using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Logistics.Application.Commands.ConfirmPickup;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Queries.GetDriverRoutesToday;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class DriverControllerTests
{
    [Fact]
    public void DriverController_RequiresDriverAuthorization()
    {
        var attr = typeof(DriverController).GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("driver");
    }

    [Fact]
    public async Task GetRoutesTodayAsync_SendsJwtDriverIdAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var driverId = Guid.NewGuid();
        sender.Send(Arg.Any<GetDriverRoutesTodayQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DriverRouteDto>>.Success([]));
        var controller = new DriverController(sender)
        {
            ControllerContext = CreateContext(driverId),
        };

        var result = await controller.GetRoutesTodayAsync(default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetDriverRoutesTodayQuery>(query => query.DriverUserId == driverId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmPickupAsync_SendsJwtDriverIdAndReturnsCreatedAsync()
    {
        var sender = Substitute.For<ISender>();
        var driverId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var orderIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        sender.Send(Arg.Any<ConfirmPickupCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ConfirmPickupResultDto>.Success(
                new ConfirmPickupResultDto(routeId, [Guid.NewGuid()])));
        var controller = new DriverController(sender)
        {
            ControllerContext = CreateContext(driverId),
        };

        var result = await controller.ConfirmPickupAsync(
            routeId,
            new ConfirmPickupRequest(orderIds),
            default);

        result.Should().BeOfType<CreatedResult>();
        await sender.Received(1).Send(
            Arg.Is<ConfirmPickupCommand>(command =>
                command.RouteId == routeId &&
                command.DriverUserId == driverId &&
                command.OrderIds.SequenceEqual(orderIds)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("ROUTE_NOT_ASSIGNED")]
    [InlineData("DELIVERY_ALREADY_EXISTS")]
    public async Task ConfirmPickupAsync_Conflict_Returns409Async(string code)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ConfirmPickupCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ConfirmPickupResultDto>.Failure(Error.Conflict(code, "conflict")));
        var controller = new DriverController(sender)
        {
            ControllerContext = CreateContext(Guid.NewGuid()),
        };

        var result = await controller.ConfirmPickupAsync(
            Guid.NewGuid(),
            new ConfirmPickupRequest([Guid.NewGuid()]),
            default);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ConfirmPickupAsync_Forbidden_Returns403Async()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ConfirmPickupCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ConfirmPickupResultDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "forbidden")));
        var controller = new DriverController(sender)
        {
            ControllerContext = CreateContext(Guid.NewGuid()),
        };

        var result = await controller.ConfirmPickupAsync(
            Guid.NewGuid(),
            new ConfirmPickupRequest([Guid.NewGuid()]),
            default);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(403);
    }

    [Theory]
    [InlineData("ORDER_NOT_AT_HUB")]
    [InlineData("ORDER_NOT_ON_ROUTE")]
    public async Task ConfirmPickupAsync_Validation_Returns422Async(string code)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ConfirmPickupCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ConfirmPickupResultDto>.Failure(Error.Validation(code, "invalid")));
        var controller = new DriverController(sender)
        {
            ControllerContext = CreateContext(Guid.NewGuid()),
        };

        var result = await controller.ConfirmPickupAsync(
            Guid.NewGuid(),
            new ConfirmPickupRequest([Guid.NewGuid()]),
            default);

        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    private static ControllerContext CreateContext(Guid userId) =>
        new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "test")),
            },
        };
}
