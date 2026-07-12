using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
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
