using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Hub.Application.Commands.ReplaceHubDriverAssignments;
using FreshFlow.Hub.Application.Commands.ReplaceHubStaffAssignments;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Queries.GetAssignedHubs;
using FreshFlow.Hub.Application.Queries.GetHubDriverAssignments;
using FreshFlow.Hub.Application.Queries.GetHubStaffAssignments;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Hub.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class HubStaffAssignmentsControllerTests
{
    [Theory]
    [InlineData(nameof(HubStaffAssignmentsController.GetAssignmentsAsync))]
    [InlineData(nameof(HubStaffAssignmentsController.ReplaceAssignmentsAsync))]
    public void ManagementEndpoints_RequireAdminOrOperationsManager(string methodName)
    {
        var attr = typeof(HubStaffAssignmentsController)
            .GetMethod(methodName)!
            .GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("admin,operations_manager");
    }

    [Fact]
    public void AssignedEndpoint_RequiresHubStaff()
    {
        var attr = typeof(HubStaffAssignmentsController)
            .GetMethod(nameof(HubStaffAssignmentsController.GetAssignedAsync))!
            .GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("hub_staff");
    }

    [Fact]
    public async Task ReplaceAssignments_SendsReplaceListAndReturnsEnvelopeAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var userIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var actorUserId = Guid.NewGuid();
        sender.Send(Arg.Any<ReplaceHubStaffAssignmentsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubStaffAssignmentsDto>.Success(
                new HubStaffAssignmentsDto(hubId, userIds)));
        var controller = CreateController(sender, actorUserId);

        var result = await controller.ReplaceAssignmentsAsync(
            hubId,
            new ReplaceHubStaffAssignmentsRequest(userIds),
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ReplaceHubStaffAssignmentsCommand>(command =>
                command.HubId == hubId &&
                command.StaffUserIds.SequenceEqual(userIds) &&
                command.ActorUserId == actorUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAssigned_SendsAuthenticatedUserIdAsync()
    {
        var sender = Substitute.For<ISender>();
        var userId = Guid.NewGuid();
        sender.Send(Arg.Any<GetAssignedHubsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HubDto>>.Success([]));
        var controller = CreateController(sender, userId);

        var result = await controller.GetAssignedAsync(default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetAssignedHubsQuery>(query => query.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(nameof(HubStaffAssignmentsController.GetDriverAssignmentsAsync))]
    [InlineData(nameof(HubStaffAssignmentsController.ReplaceDriverAssignmentsAsync))]
    public void DriverEndpoints_RequireAdminOrOperationsManager(string methodName)
    {
        var attr = typeof(HubStaffAssignmentsController)
            .GetMethod(methodName)!
            .GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("admin,operations_manager");
    }

    [Fact]
    public async Task ReplaceDriverAssignments_SendsReplaceListAndReturnsEnvelopeAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var userIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var actorUserId = Guid.NewGuid();
        sender.Send(Arg.Any<ReplaceHubDriverAssignmentsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubDriverAssignmentsDto>.Success(
                new HubDriverAssignmentsDto(hubId, userIds)));
        var controller = CreateController(sender, actorUserId);

        var result = await controller.ReplaceDriverAssignmentsAsync(
            hubId,
            new ReplaceHubDriverAssignmentsRequest(userIds),
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ReplaceHubDriverAssignmentsCommand>(command =>
                command.HubId == hubId &&
                command.DriverUserIds.SequenceEqual(userIds) &&
                command.ActorUserId == actorUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDriverAssignments_SendsQueryWithActorUserIdAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        sender.Send(Arg.Any<GetHubDriverAssignmentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubDriverAssignmentsDto>.Success(
                new HubDriverAssignmentsDto(hubId, [])));
        var controller = CreateController(sender, actorUserId);

        var result = await controller.GetDriverAssignmentsAsync(hubId, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetHubDriverAssignmentsQuery>(query =>
                query.HubId == hubId && query.ActorUserId == actorUserId),
            Arg.Any<CancellationToken>());
    }

    private static HubStaffAssignmentsController CreateController(ISender sender, Guid userId) =>
        new(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                        "Test"))
                }
            }
        };
}
