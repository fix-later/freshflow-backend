using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Notifications.Application.Commands.RegisterDevice;
using FreshFlow.Notifications.Application.Commands.UnregisterDevice;
using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class NotificationDeviceControllerTests
{
    [Fact]
    public async Task RegisterDeviceAsync_UsesUserIdFromJwtAsync()
    {
        var sender = Substitute.For<ISender>();
        var userId = Guid.NewGuid();
        var dto = new NotificationDeviceDto(
            Guid.NewGuid(),
            userId,
            "push-token",
            "ios",
            "device-1",
            DateTime.UtcNow,
            DateTime.UtcNow,
            null);
        sender.Send(Arg.Any<RegisterDeviceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<NotificationDeviceDto>.Success(dto));
        var controller = new NotificationDeviceController(sender)
        {
            ControllerContext = CreateControllerContext(userId)
        };

        var result = await controller.RegisterDeviceAsync(
            new RegisterNotificationDeviceRequest("push-token", "ios", "device-1"),
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<RegisterDeviceCommand>(command =>
                command.UserId == userId &&
                command.Token == "push-token" &&
                command.Platform == "ios" &&
                command.DeviceId == "device-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnregisterDeviceAsync_Success_UsesUserIdFromJwtAsync()
    {
        var sender = Substitute.For<ISender>();
        var userId = Guid.NewGuid();
        var dto = new NotificationDeviceDto(
            Guid.NewGuid(),
            userId,
            "push-token",
            "web",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            DateTime.UtcNow);
        sender.Send(Arg.Any<UnregisterDeviceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<NotificationDeviceDto>.Success(dto));
        var controller = new NotificationDeviceController(sender)
        {
            ControllerContext = CreateControllerContext(userId)
        };

        var result = await controller.UnregisterDeviceAsync("push-token", default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<UnregisterDeviceCommand>(command =>
                command.UserId == userId &&
                command.Token == "push-token"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnregisterDeviceAsync_NotFound_Returns404Async()
    {
        var sender = Substitute.For<ISender>();
        var userId = Guid.NewGuid();
        sender.Send(Arg.Any<UnregisterDeviceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<NotificationDeviceDto>.Failure(
                Error.NotFound("NotificationDevice", "provided token")));
        var controller = new NotificationDeviceController(sender)
        {
            ControllerContext = CreateControllerContext(userId)
        };

        var result = await controller.UnregisterDeviceAsync("missing-token", default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    private static ControllerContext CreateControllerContext(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "Test");

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
