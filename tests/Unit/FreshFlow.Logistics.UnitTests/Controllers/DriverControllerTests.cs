using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Logistics.Application.Commands.AttachProofOfDelivery;
using FreshFlow.Logistics.Application.Commands.ConfirmPickup;
using FreshFlow.Logistics.Application.Commands.CreateProofUploadSignature;
using FreshFlow.Logistics.Application.Commands.StartRoute;
using FreshFlow.Logistics.Application.Commands.UpdateDeliveryStatus;
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

    [Fact]
    public async Task StartRouteAsync_SendsJwtDriverIdAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var driverId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        sender.Send(Arg.Any<StartRouteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<StartRouteResultDto>.Success(
                new StartRouteResultDto(routeId, "in_progress", 2)));
        var controller = new DriverController(sender)
        {
            ControllerContext = CreateContext(driverId),
        };

        var result = await controller.StartRouteAsync(routeId, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<StartRouteCommand>(command =>
                command.RouteId == routeId &&
                command.DriverUserId == driverId),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("ROUTE_NOT_STARTABLE")]
    [InlineData("ROUTE_HAS_NO_DELIVERIES")]
    [InlineData("PENDING_HUB_DISCREPANCY")]
    public async Task StartRouteAsync_Conflict_Returns409Async(string code)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<StartRouteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<StartRouteResultDto>.Failure(Error.Conflict(code, "conflict")));
        var controller = new DriverController(sender)
        {
            ControllerContext = CreateContext(Guid.NewGuid()),
        };

        var result = await controller.StartRouteAsync(Guid.NewGuid(), default);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateProofUploadSignatureAsync_SendsJwtDriverIdAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var driverId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        sender.Send(Arg.Any<CreateProofUploadSignatureCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UploadSignatureResponse>.Success(
                new UploadSignatureResponse("sig", 123, "key", "cloud", "freshflow/proof-of-delivery")));
        var controller = new DriverController(sender)
        {
            ControllerContext = CreateContext(driverId),
        };

        var result = await controller.CreateProofUploadSignatureAsync(deliveryId, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<CreateProofUploadSignatureCommand>(command =>
                command.DeliveryId == deliveryId &&
                command.DriverUserId == driverId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AttachProofOfDeliveryAsync_SendsJwtDriverIdAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var driverId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var proofUrl = "https://res.cloudinary.com/demo/image/upload/pod.jpg";
        sender.Send(Arg.Any<AttachProofOfDeliveryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AttachProofOfDeliveryResponse>.Success(
                new AttachProofOfDeliveryResponse(deliveryId, proofUrl)));
        var controller = new DriverController(sender)
        {
            ControllerContext = CreateContext(driverId),
        };

        var result = await controller.AttachProofOfDeliveryAsync(
            deliveryId,
            new AttachProofOfDeliveryRequest(proofUrl),
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<AttachProofOfDeliveryCommand>(command =>
                command.DeliveryId == deliveryId &&
                command.DriverUserId == driverId &&
                command.ProofUrl == proofUrl),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateDeliveryStatusAsync_SendsJwtDriverIdAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var driverId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        sender.Send(Arg.Any<UpdateDeliveryStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UpdateDeliveryStatusResponse>.Success(
                new UpdateDeliveryStatusResponse(deliveryId, Guid.NewGuid(), "arrived", DateTime.UtcNow, null)));
        var controller = new DriverController(sender)
        {
            ControllerContext = CreateContext(driverId),
        };

        var result = await controller.UpdateDeliveryStatusAsync(
            deliveryId,
            new UpdateDeliveryStatusRequest("ARRIVED", null),
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<UpdateDeliveryStatusCommand>(command =>
                command.DeliveryId == deliveryId &&
                command.DriverUserId == driverId &&
                command.Status == "ARRIVED" &&
                command.FailureReason == null),
            Arg.Any<CancellationToken>());
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
