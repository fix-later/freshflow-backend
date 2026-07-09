using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Notifications.Application.Commands.MarkNotificationRead;
using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.Notifications.Application.Queries.ListNotifications;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class NotificationControllerTests
{
    [Fact]
    public void NotificationController_RequiresAuthorization()
    {
        var attr = typeof(NotificationController).GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
    }

    [Fact]
    public async Task ListAsync_UsesUserIdFromJwtAndReturnsPagedEnvelopeAsync()
    {
        var sender = Substitute.For<ISender>();
        var userId = Guid.NewGuid();
        var dto = CreateDto(userId: userId);
        var page = new NotificationPageDto([dto], 25, "next-cursor");
        sender.Send(Arg.Any<ListNotificationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<NotificationPageDto>.Success(page));
        var controller = new NotificationController(sender)
        {
            ControllerContext = CreateControllerContext(userId)
        };

        var result = await controller.ListAsync(
            cursor: "cursor-1",
            pageSize: 25,
            isRead: false,
            default);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().NotBeNull();
        ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value).Should().BeEquivalentTo(page.Items);
        ok.Value.GetType().GetProperty("meta")!.GetValue(ok.Value).Should().NotBeNull();
        await sender.Received(1).Send(
            Arg.Is<ListNotificationsQuery>(query =>
                query.UserId == userId &&
                query.Cursor == "cursor-1" &&
                query.PageSize == 25 &&
                query.IsRead == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_WithoutValidJwt_ThrowsUnauthorizedAccessExceptionAsync()
    {
        var sender = Substitute.For<ISender>();
        var controller = new NotificationController(sender)
        {
            ControllerContext = CreateControllerContext(userId: null)
        };

        Func<Task> act = () => controller.ListAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await sender.DidNotReceive().Send(
            Arg.Any<ListNotificationsQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkReadAsync_Success_UsesUserIdFromJwtAsync()
    {
        var sender = Substitute.For<ISender>();
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var dto = CreateDto(notificationId, userId);
        sender.Send(Arg.Any<MarkNotificationReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<NotificationDto>.Success(dto));
        var controller = new NotificationController(sender)
        {
            ControllerContext = CreateControllerContext(userId)
        };

        var result = await controller.MarkReadAsync(notificationId, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<MarkNotificationReadCommand>(command =>
                command.UserId == userId &&
                command.NotificationId == notificationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkReadAsync_NotFound_Returns404Async()
    {
        var sender = Substitute.For<ISender>();
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        sender.Send(Arg.Any<MarkNotificationReadCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<NotificationDto>.Failure(Error.NotFound("NOTIFICATION", notificationId)));
        var controller = new NotificationController(sender)
        {
            ControllerContext = CreateControllerContext(userId)
        };

        var result = await controller.MarkReadAsync(notificationId, default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    private static ControllerContext CreateControllerContext(Guid? userId)
    {
        var claims = userId.HasValue
            ? [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())]
            : Array.Empty<Claim>();
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    private static NotificationDto CreateDto(Guid? id = null, Guid? userId = null) =>
        new(
            id ?? Guid.NewGuid(),
            "order_status",
            "Order confirmed",
            $"Notification for {userId ?? Guid.NewGuid()}",
            """{"new_status":"confirmed"}""",
            false,
            null,
            DateTime.UtcNow);
}
