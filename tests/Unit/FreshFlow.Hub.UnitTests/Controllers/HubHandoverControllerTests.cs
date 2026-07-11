using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Hub.Application.Commands.CreateHandover;
using FreshFlow.Hub.Application.Commands.DriverCheckout;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Queries.ListHandovers;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Hub.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class HubHandoverControllerTests
{
    [Fact]
    public void CreateHandoverAsync_RequiresHubStaffAdminOrOperationsManagerAuthorization()
    {
        var attr = typeof(HubHandoverController)
            .GetMethod(nameof(HubHandoverController.CreateHandoverAsync))!
            .GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("hub_staff,admin,operations_manager");
    }

    [Fact]
    public void DriverCheckoutAsync_RequiresDriverRoleOnly()
    {
        var attr = typeof(HubHandoverController)
            .GetMethod(nameof(HubHandoverController.DriverCheckoutAsync))!
            .GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("driver");
    }

    [Fact]
    public void HubHandoverController_DoesNotHaveClassLevelAuthorize()
    {
        typeof(HubHandoverController)
            .GetCustomAttribute<AuthorizeAttribute>()
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task CreateHandoverAsync_Success_SendsCommandWithAuthenticatedUserAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var outboundId = Guid.NewGuid();
        var handedOverBy = Guid.NewGuid();
        sender.Send(Arg.Any<CreateHandoverCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubHandoverDto>.Success(CreateDto(hubId, routeId, driverUserId)));
        var controller = CreateController(sender, handedOverBy);

        var result = await controller.CreateHandoverAsync(
            hubId,
            new CreateHandoverRequest(routeId, driverUserId, outboundId, "Ready"),
            default);

        result.Should().BeOfType<CreatedResult>();
        await sender.Received(1).Send(
            Arg.Is<CreateHandoverCommand>(command =>
                command.HubId == hubId &&
                command.DeliveryRouteId == routeId &&
                command.DriverUserId == driverUserId &&
                command.OutboundEventId == outboundId &&
                command.HandedOverBy == handedOverBy &&
                command.Notes == "Ready"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DriverCheckoutAsync_Success_SendsCommandWithAuthenticatedDriverAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var handoverId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        sender.Send(Arg.Any<DriverCheckoutCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubHandoverDto>.Success(CreateDto(hubId, Guid.NewGuid(), driverUserId)));
        var controller = CreateController(sender, driverUserId);

        var result = await controller.DriverCheckoutAsync(hubId, handoverId, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<DriverCheckoutCommand>(command =>
                command.HubId == hubId &&
                command.HandoverId == handoverId &&
                command.DriverUserId == driverUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DriverCheckoutAsync_ForbiddenResult_Returns403Async()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<DriverCheckoutCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubHandoverDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "Not assigned.")));
        var controller = CreateController(sender, Guid.NewGuid());

        var result = await controller.DriverCheckoutAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ListHandoversAsync_Success_SendsQueryAndReturnsPagedEnvelopeAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        sender.Send(Arg.Any<ListHandoversQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubHandoverPageDto>.Success(
                new HubHandoverPageDto([CreateDto(hubId, Guid.NewGuid(), Guid.NewGuid())], 25, "next")));
        var controller = CreateController(sender, Guid.NewGuid());

        var result = await controller.ListHandoversAsync(hubId, "cursor", 25, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ListHandoversQuery>(query =>
                query.HubId == hubId &&
                query.Cursor == "cursor" &&
                query.PageSize == 25),
            Arg.Any<CancellationToken>());
    }

    private static HubHandoverController CreateController(ISender sender, Guid userId) =>
        new(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())]))
                }
            }
        };

    private static HubHandoverDto CreateDto(Guid hubId, Guid routeId, Guid driverUserId) =>
        new(
            Guid.NewGuid(),
            hubId,
            routeId,
            driverUserId,
            null,
            HubHandoverEvent.StatusPendingCheckout,
            Guid.NewGuid(),
            DateTime.UtcNow,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);
}
