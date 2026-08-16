using System.Reflection;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Queries.GetRouteDeliveries;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class RouteDeliveriesControllerTests
{
    [Fact]
    public void GetRouteDeliveries_RequiresAdminOrOperationsManager()
    {
        var authorize = typeof(RoutesController)
            .GetMethod(nameof(RoutesController.GetRouteDeliveriesAsync))!
            .GetCustomAttribute<AuthorizeAttribute>();

        authorize!.Roles.Should().Be("admin,operations_manager");
    }

    [Fact]
    public async Task GetRouteDeliveries_SendsQueryAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var routeId = Guid.NewGuid();
        sender.Send(Arg.Any<GetRouteDeliveriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DriverDeliveryDto>>.Success([]));
        var controller = new RoutesController(sender);

        var result = await controller.GetRouteDeliveriesAsync(routeId, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetRouteDeliveriesQuery>(query => query.RouteId == routeId),
            Arg.Any<CancellationToken>());
    }
}
